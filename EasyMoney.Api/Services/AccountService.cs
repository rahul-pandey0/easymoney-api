using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EasyMoney.Api.Services;

public interface IAccountService
{
    Task<Account> OpenAccountAsync(long memberId, decimal monthlyContribution, DateOnly openDate, long? createdBy, long? authorizedBy, AccountOpenPayload payload);
    Task<Account> UpdateAccountAsync(long accountId, long memberId, decimal monthlyContribution, DateOnly openDate, long? updateBy, long? authorizedBy, AccountUpdatePayload payload);
     
    Task<AccountSummaryDto> GetSummaryAsync(long accountId); 
    Task<IReadOnlyList<AccountSummaryDto>> ListByMemberAsync(long memberId); 
    Task<IReadOnlyList<AccountSummaryDto>> ListByTenantAsync(int skip, int take);

    /// <summary>Transition account.status to COMPLETED. Called by MonthlyCycleJob on tenure end.</summary>
    Task CompleteTenureAsync(long accountId);
    Task<Account> GetMemberAsync(int schemeId, long memberId);


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
            BranchId =member.BranchId,
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
            SchemeId = req.SchemeId,
            CustomerCode =req.CustomerCode,
            IsBidding="N",
            FirstPaymentFlag= "N"


        };
        _db.Accounts.Add(acct);
        await _db.SaveChangesAsync();

        // Generate all dues upfront for the full tenure so the payment schedule is visible immediately
        await _ledger.GenerateAllDuesForAccountAsync(acct.AccountId);

        _log.LogInformation("Opened account {Aid} {Num} for member {Mid} (tenant {Tid}, ₹{Cont}/mo, tenure {End})",
            acct.AccountId, accountNumber, memberId, member.TenantId, monthlyContribution, tenureEnd);
        return acct;
    }


    public async Task<Account> UpdateAccountAsync(
    long accountId,
    long memberId,
    decimal monthlyContribution,
    DateOnly openDate,
    long? updateBy,
    long? authorizedBy,
    AccountUpdatePayload req)
    {
        if (monthlyContribution <= 0)
            throw new DomainException("Monthly contribution must be > 0");

        // Get the existing account (NOT create a new one)
        var account = await _db.Accounts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.AccountId == accountId && a.MemberId == memberId)
            ?? throw new DomainException($"Account {accountId} not found for member {memberId}");

        //// Check if account can be updated
        //if (account.Status == AccountStatus.CLOSED || account.Status == AccountStatus.CANCELLED)
        //    throw new DomainException($"Cannot update account with status: {account.Status}");

        // Get member for validation
        var member = await _db.Members
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.MemberId == memberId)
            ?? throw new DomainException($"Member {memberId} not found");

        // Get scheme configuration
        var scheme = await _db.SchemeConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.SchemeId == req.SchemeId && s.TenantId == member.TenantId)
            ?? throw new DomainException($"Scheme {req.SchemeId} not found for tenant {member.TenantId}");

        // KYC validation
        if (member.KycStatus != KycStatus.APPROVED)
            throw new DomainException($"Member KYC must be APPROVED to update account (current: {member.KycStatus})");

        if (scheme.KycMode == KycMode.FULL_ONLY && member.KycTier != KycTier.FULL)
            throw new DomainException("Tenant policy (FULL_ONLY) requires kyc_tier=FULL");



        // Store old values for audit
        var oldValues = new
        {
            MonthlyContribution = account.MonthlyContribution,
            AccountOpenDate = account.AccountOpenDate,
            SchemeId = account.SchemeId,
            InterestRate = account.InterestRate,
            TargetAmount = account.TargetAmount,
            Status = account.Status,
            InstallmentsPaid = account.InstallmentsPaid,
            IsPrized = account.IsPrized
        };

        // ⚠️ DO NOT update AccountNumber - it should remain unchanged
        // account.AccountNumber = req.AccountNumber; // ❌ REMOVE THIS LINE

        // Update all other fields
        account.MonthlyContribution = req.MonthlyContribution;
        account.AccountOpenDate = req.AccountOpenDate;
        account.TenureEndDate = req.TenureEndDate ;
        account.OldAccountNo = req.OldAccountNo ?? account.OldAccountNo;
        account.PhoneNo = req.PhoneNo ?? account.PhoneNo;
        account.CustomerName = req.CustomerName ?? account.CustomerName;
        account.InterestRate = req.InterestRate;
        account.TargetAmount = req.TargetAmount;
        account.PaymentDate = req.PaymentDate ?? account.PaymentDate;
        account.PaidAmount = req.PaidAmount;
        account.LoanAmount = req.LoanAmount;
        account.BonusAmount = req.BonusAmount;
        account.InterestAmount = req.InterestAmount;
        account.TotalAmount = req.TotalAmount;
        account.Remarks = req.Remarks ?? account.Remarks;
        account.SchemeId = req.SchemeId;

        // Update status if provided
        //if (!string.IsNullOrEmpty(req.Status))
        //{
        //    var newStatus = Enum.Parse<AccountStatus>(req.Status);
        //    if (account.Status != newStatus)
        //    {
        //        ValidateStatusTransition(account.Status, newStatus);
        //        account.Status = newStatus;

        //        if (newStatus == AccountStatus.CLOSED || newStatus == AccountStatus.CANCELLED)
        //        {
        //            account.ClosureDate = req.ClosureDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        //        }
        //    }
        //}

        // Update installments paid
        if (req.InstallmentsPaid.HasValue)
        {
            if (req.InstallmentsPaid.Value < 0)
                throw new DomainException("Installments paid cannot be negative");

            if (req.InstallmentsPaid.Value > scheme.TenureMonths)
                throw new DomainException($"Installments paid cannot exceed tenure months ({scheme.TenureMonths})");

            account.InstallmentsPaid = req.InstallmentsPaid.Value;
        }

        // Update prized status
        if (req.IsPrized.HasValue)
        {
            account.IsPrized = req.IsPrized.Value;
        }

        // Update audit fields
        account.UpdatedAt = DateTime.UtcNow;
        account.UpdatedBy = updateBy ?? _ctx.UserId;

        // Save changes
        await _db.SaveChangesAsync();

        // Log the update
        _log.LogInformation(
            "Updated account {AccountId} for member {MemberId}. Old values: {@OldValues}, New values: {@NewValues}",
            account.AccountId,
            memberId,
            oldValues,
            new
            {
                account.AccountNumber, // Still shows the unchanged account number
                account.MonthlyContribution,
                account.AccountOpenDate,
                account.SchemeId,
                account.InterestRate,
                account.TargetAmount,
                account.Status,
                account.InstallmentsPaid,
                account.IsPrized
            }
        );

        return account;
    }

    public async Task<Account> UpdateAccountAsync1(long accountId,long memberId, decimal monthlyContribution, DateOnly openDate, long? updateBy, long? authorizedBy, AccountUpdatePayload req)
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

        var acct = new Account
        {
            TenantId = member.TenantId,
            MemberId = memberId,
            AccountNumber = req.AccountNumber,
            MonthlyContribution = monthlyContribution,
            AccountOpenDate = openDate,
            TenureEndDate = req.TenureEndDate,
            Status = AccountStatus.ACTIVE,
            InstallmentsPaid = 0,
            IsPrized = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = updateBy ?? _ctx.UserId,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = updateBy ?? _ctx.UserId,
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
        _db.Accounts.Update(acct);
        await _db.SaveChangesAsync();

        _log.LogInformation("Opened account {Aid} {Num} for member {Mid} (tenant {Tid}, ₹{Cont}/mo, tenure {End})",
            acct.AccountId, req.AccountNumber, memberId, member.TenantId, monthlyContribution, req.TenureEndDate);
        return acct;
    }

    public async Task<AccountSummaryDto> GetSummaryAsync(long accountId)
    {
        var a = await _db.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.AccountId == accountId)
            ?? throw new DomainException($"Account {accountId} not found");
        return await BuildSummaryAsync(a);
    }

    public async Task<Account> GetMemberAsync(int schemeId, long memberId)
    {
        var account = await _db.Accounts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.SchemeId == schemeId && x.MemberId == memberId);

        return account; // Returns null or the entity
    }
    //public async Task<AccountSummaryDto> GetMemberAsync(int schemeId, long memberId)
    //{
    //    var account = await _db.Accounts
    //        .IgnoreQueryFilters()
    //        .FirstOrDefaultAsync(x => x.SchemeId == schemeId && x.MemberId == memberId);
    //        //?? throw new DomainException($"Member with ID {memberId} not found in scheme {schemeId}");

    //    return(account);
    //}
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
            a.TotalAmount, a.Remarks ,a.SchemeId,a.BranchId,a.IsBidding ,a.CustomerCode);   
    }
}
