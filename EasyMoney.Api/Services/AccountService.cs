using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EasyMoney.Api.Services;

public interface IAccountService
{
    Task<Account> OpenAccountAsync(long memberId, decimal monthlyContribution, DateOnly openDate, long? createdBy, long? authorizedBy, AccountOpenPayload payload);

    Task<AccountSummaryDto> GetSummaryAsync(long accountId);
    Task<IReadOnlyList<AccountSummaryDto>> ListByMemberAsync(long memberId);
    Task<IReadOnlyList<AccountSummaryDto>> ListByTenantAsync(int skip, int take);

    /// <summary>Transition account.status to COMPLETED. Called by MonthlyCycleJob on tenure end.</summary>
    Task CompleteTenureAsync(long accountId);
}

public class AccountService : IAccountService
{
    private readonly EasyMoneyDbContext _db;
    private readonly ITenantContext _ctx;
    private readonly IAccountingService _accounting;
    private readonly ILedgerService _ledger;
    private readonly ILogger<AccountService> _log;

    public AccountService(EasyMoneyDbContext db, ITenantContext ctx, IAccountingService accounting, ILedgerService ledger, ILogger<AccountService> log)
    {
        _db = db; _ctx = ctx; _accounting = accounting; _ledger = ledger; _log = log;
    }

    public async Task<Account> OpenAccountAsync(long memberId, decimal monthlyContribution, DateOnly openDate, long? createdBy, long? authorizedBy, AccountOpenPayload req)
    {
        if (monthlyContribution <= 0) throw new DomainException("Monthly contribution must be > 0");

        var member = await _db.Members.IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.MemberId == memberId)
            ?? throw new DomainException($"Member {memberId} not found");

        // KYC gate (always: must be APPROVED). Tier requirement is enforced via scheme_config.kyc_mode.
        if (member.KycStatus != KycStatus.APPROVED)
            throw new DomainException($"Member KYC must be APPROVED to open account (current: {member.KycStatus})");

        var scheme = await _db.SchemeConfigs.IgnoreQueryFilters()
            .FirstAsync(s => s.TenantId == member.TenantId);
        if (scheme.KycMode == KycMode.FULL_ONLY && member.KycTier != KycTier.FULL)
            throw new DomainException("Tenant policy (FULL_ONLY) requires kyc_tier=FULL");

        // Account number: EM-{tenantId}-{6-digit-seq}
        var seq = await _db.Accounts.IgnoreQueryFilters()
            .Where(a => a.TenantId == member.TenantId).CountAsync() + 1;
        //var accountNumber = $"EM-{member.TenantId}-{seq:D6}";

        var accountNumber = $"{member.TenantId}{scheme.SchemeCode}{seq:D6}";

        var tenureEnd = openDate.AddMonths(scheme.TenureMonths);

        var acct = new Account
        {
            TenantId = member.TenantId,
            MemberId = memberId,
            AccountNumber = accountNumber,
            MonthlyContribution = monthlyContribution,
            AccountOpenDate = openDate,
            TenureEndDate = tenureEnd,
            Status = AccountStatus.ACTIVE,
            InstallmentsPaid = 0,
            IsPrized = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy ?? _ctx.UserId,
            AuthorizedBy = authorizedBy,
            AuthorizedAt = authorizedBy.HasValue ? DateTime.UtcNow : null,

            OldAccountNo = req.OldAccountNo,
            PhoneNo = req.PhoneNo,
            CustomerName = req.CustomerName,
            InterestRate = req.InterestRate,
            TargetAmount = req.TargetAmount,
            PaymentDate = req.PaymentDate,
            PaidAmount = req.PaidAmount,
            LoanAmount = req.LoanAmount,
            BonusAmount = req.BonusAmount,
            InterestAmount = req.InterestAmount,
            TotalAmount = req.TotalAmount,
            Remarks = req.Remarks,
            SchemeId = req.SchemeId

        };
        _db.Accounts.Add(acct);
        await _db.SaveChangesAsync();

        // Generate all dues upfront for the full tenure so the payment schedule is visible immediately
        await _ledger.GenerateAllDuesForAccountAsync(acct.AccountId);

        _log.LogInformation("Opened account {Aid} {Num} for member {Mid} (tenant {Tid}, ₹{Cont}/mo, tenure {End})",
            acct.AccountId, accountNumber, memberId, member.TenantId, monthlyContribution, tenureEnd);
        return acct;
    }

    public async Task<AccountSummaryDto> GetSummaryAsync(long accountId)
    {
        var a = await _db.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.AccountId == accountId)
            ?? throw new DomainException($"Account {accountId} not found");
        return await BuildSummaryAsync(a);
    }

    public async Task<IReadOnlyList<AccountSummaryDto>> ListByMemberAsync(long memberId)
    {
        var accts = await _db.Accounts.IgnoreQueryFilters()
            .Where(a => a.MemberId == memberId).OrderBy(a => a.AccountId).ToListAsync();
        var result = new List<AccountSummaryDto>();
        foreach (var a in accts) result.Add(await BuildSummaryAsync(a));
        return result;
    }

    public async Task<IReadOnlyList<AccountSummaryDto>> ListByTenantAsync(int skip, int take)
    {
        var accts = await _db.Accounts
            .OrderBy(a => a.AccountId).Skip(skip).Take(Math.Clamp(take, 1, 200))
            .ToListAsync();
        var result = new List<AccountSummaryDto>();
        foreach (var a in accts) result.Add(await BuildSummaryAsync(a));
        return result;
    }

    public async Task CompleteTenureAsync(long accountId)
    {
        var a = await _db.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.AccountId == accountId)
            ?? throw new DomainException($"Account {accountId} not found");
        if (a.Status is AccountStatus.COMPLETED or AccountStatus.EXITED) return;
        a.Status = AccountStatus.COMPLETED;
        await _db.SaveChangesAsync();
        _log.LogInformation("Account {Aid} tenure completed", accountId);
    }

    private async Task<AccountSummaryDto> BuildSummaryAsync(Account a)
    {
        var scheme = await _db.SchemeConfigs.IgnoreQueryFilters().FirstAsync(s => s.TenantId == a.TenantId);
        var corpus = await _accounting.GetMemberAccountBalanceAsync(a.AccountId);
        var loan = await _db.Loans.IgnoreQueryFilters().FirstOrDefaultAsync(l => l.AccountId == a.AccountId);

        bool eligibleToBid = a.Status == AccountStatus.ACTIVE
            && !a.IsPrized
            && a.InstallmentsPaid >= scheme.MinInstallmentsForEligibility;
        bool eligibleForDividend = a.Status is AccountStatus.ACTIVE or AccountStatus.PRIZED
            && a.InstallmentsPaid >= scheme.MinInstallmentsForEligibility;

        return new AccountSummaryDto(
            a.AccountId, a.AccountNumber, a.MemberId, a.TenantId,
            a.MonthlyContribution, a.AccountOpenDate, a.TenureEndDate,
            a.Status.ToString(), a.InstallmentsPaid, a.IsPrized,
            eligibleToBid, eligibleForDividend,
            corpus, loan?.CycleId,a.CreatedAt, a.OldAccountNo, a.PhoneNo, a.CustomerName, a.InterestRate, a.TargetAmount,
            a.PaymentDate, a.PaidAmount,a.LoanAmount,a.BonusAmount, a.InterestAmount,
            a.TotalAmount, a.Remarks ,a.SchemeId);   
    }
}
