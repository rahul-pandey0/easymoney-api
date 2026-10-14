using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Reflection.Emit;
using MySql.Data.MySqlClient;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OfficeOpenXml;
using System.Configuration;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using Microsoft.AspNetCore.Mvc;

namespace EasyMoney.Api.Services;

public interface IAccountService
{
    Task<Account> OpenAccountAsync(long memberId, decimal monthlyContribution, DateOnly openDate, long? createdBy, long? authorizedBy, AccountOpenPayload payload);
    Task<Account> UpdateAccountAsync(long accountId, long memberId, decimal monthlyContribution, DateOnly openDate, long? updateBy, long? authorizedBy, AccountUpdatePayload payload);
     
    Task<AccountSummaryDto> GetSummaryAsync(long accountId); 
    Task<AccountSummaryDto> GetAccountAsync(long accountId); 
    Task<IReadOnlyList<AccountSummaryDto>> ListByMemberAsync(long memberId); 
    Task<IReadOnlyList<AccountSummaryDto>> ListByTenantAsync(int skip, int take);
    Task<IReadOnlyList<AccountDto>> ListledgerEntrydata(int skip, int take); 
    Task<IReadOnlyList<AccountSummaryDto>> ListByAccountclosed(int skip, int take); 


    /// <summary>Transition account.status to COMPLETED. Called by MonthlyCycleJob on tenure end.</summary>
    Task CompleteTenureAsync(long accountId);
    Task<Account> GetMemberAsync(int schemeId, long memberId); 

    Task<AccountClosure> ClosedAccountAsync(AccountClosureRequest payload);
    Task<SifinCommission> SifinAccountAsync(SifinCommission payload);
    Task<SifinCommission> GetsifinSummaryAsync();
    Task<PoolMoneySummary> GetPoolMoneyAsync();

    Task<Account> UpdatePeriodChangeAsync(long accountId, PeriodChangeRequest request);

    Task<(bool isSuccess, int rowCount, string errorCsv)> InsertvalidLedgerData(IFormFile file, string fileExtension, string batchCode);
    Task<(bool isSuccess, int rowCount, string errorCsv)> InsertvalidAccData(IFormFile file, string fileExtension, string batchCode); 
    Task<(bool isSuccess, int rowCount, string errorCsv)> InsertvalidApaymentData(IFormFile file, string fileExtension, string batchCode);
    Task<(bool isSuccess, string errorMessage)> InsertToDatabase(string batchCode);
    Task<(bool isSuccess, string errorMessage)> InsertToPaymentDatabase(string batchCode); 
    Task<(bool isSuccess, string errorMessage)> InsertToLedgerDatabase(string batchCode);

    //Task<(bool isSuccess, string errorMessage)> InsertToDatabase(string batchCode); 

}

public class AccountService : IAccountService
{
    private readonly EasyMoneyDbContext _db;
    private readonly ITenantContext _ctx;
    private readonly IAccountingService _accounting;
    private readonly ILedgerService _ledger;
    private readonly ILogger<AccountService> _log;
    private readonly IConfiguration _configuration;

    public AccountService(EasyMoneyDbContext db, ITenantContext ctx, IAccountingService accounting, ILedgerService ledger, ILogger<AccountService> log, IConfiguration configuration)
    {
        _db = db; _ctx = ctx; _accounting = accounting; _ledger = ledger; _log = log; _configuration = configuration;

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

        //var accountNumber = $"{member.TenantId}{member.BranchId}{seq:D6}";

        var tenureEnd = openDate.AddMonths(scheme.TenureMonths);

        var acct = new Account
        {
            TenantId = member.TenantId,
            BranchId = member.BranchId,
            MemberId = memberId,
            AccountNumber = await GenerateAccountNumberAsync(member.TenantId, member.BranchId),
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
            CustomerCode = req.CustomerCode,
            IsBidding = "N",
            FirstPaymentFlag = "N",
            Tenure = req.Tenure

        };
        _db.Accounts.Add(acct);
        await _db.SaveChangesAsync();

        // Generate all dues upfront for the full tenure so the payment schedule is visible immediately
        await _ledger.GenerateAllDuesForAccountAsync(acct.AccountId);

        _log.LogInformation("Opened account {Aid} {Num} for member {Mid} (tenant {Tid}, ₹{Cont}/mo, tenure {End})",
            acct.AccountId,  memberId, member.TenantId, monthlyContribution, tenureEnd);
        return acct;
    }
    public async Task<string> GenerateAccountNumberAsync(long tenantId, long? branchId)
    {
        if (!branchId.HasValue)
            throw new ArgumentNullException(nameof(branchId));

        var conn = _db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "next_branch_account_seq";
        cmd.CommandType = CommandType.StoredProcedure;

        if (_db.Database.CurrentTransaction is { } efTx)
            cmd.Transaction = efTx.GetDbTransaction();

        var pT = cmd.CreateParameter(); pT.ParameterName = "@p_tenant_id"; pT.Value = tenantId; cmd.Parameters.Add(pT);
        var pB = cmd.CreateParameter(); pB.ParameterName = "@p_branch_id"; pB.Value = branchId.Value; cmd.Parameters.Add(pB);

        var pOut = cmd.CreateParameter();
        pOut.ParameterName = "@p_seq";
        pOut.DbType = DbType.UInt64;
        pOut.Direction = ParameterDirection.Output;
        cmd.Parameters.Add(pOut);

        await cmd.ExecuteNonQueryAsync();

        long nextSeq = Convert.ToInt64(pOut.Value);
        return $"{tenantId}{branchId.Value}{nextSeq:D6}";
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

    public async Task<AccountSummaryDto> GetAccountAsync(long accountId)
    {
        var a = await _db.Accounts .Where(a => a.TenantId == _ctx.TenantId && a.BranchId == _ctx.BranchId).IgnoreQueryFilters().FirstOrDefaultAsync(x => x.AccountId == accountId && x.TenantId==_ctx.TenantId)
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
               .Where(l => l.TenantId == _ctx.TenantId && l.BranchId == _ctx.BranchId)
            .OrderByDescending(a => a.AccountId).Skip(skip).Take(Math.Clamp(take, 1, 999999999)) 
            .ToListAsync();
        var result = new List<AccountSummaryDto>();
        foreach (var a in accts) result.Add(await BuildSummaryAsync(a));
        return result; 
    }
    //public async Task<IReadOnlyList<AccountSummaryDto>> ListledgerEntrydata(int skip, int take)
    //{
    //    // 🔹 Take the login working date (28-08-2026)
    //    var loginDate = _ctx.PreviousDate;          // or _ctx.CurrentDate, whichever you want as "today"

    //    // 🔹 Derive month start and next-month start from it
    //    var monthStart = new DateOnly(loginDate.Year, loginDate.Month, 1);        // 2026-08-01
    //    var nextMonthStart = monthStart.AddMonths(1);                                // 2026-09-01

    //    var monthStartDt = monthStart.ToDateTime(TimeOnly.MinValue);                 // 2026-08-01 00:00
    //    var monthEndDt = nextMonthStart.ToDateTime(TimeOnly.MinValue);             // 2026-09-01 00:00 (exclusive)

    //    var accts = await _db.LedgerEntries
    //        .AsNoTracking()
    //        .Where(x => x.TenantId == _ctx.TenantId
    //                 && x.BranchId == _ctx.BranchId
    //                 && x.EntryDate.ToDateTime(TimeOnly.MinValue) >= monthStartDt
    //                 && x.EntryDate.ToDateTime(TimeOnly.MinValue) < monthEndDt
    //                 && x.EntryType == LedgerEntryType.PAYMENT_RECEIVED)
    //        .OrderBy(x => x.AccountId)
    //        .Skip(skip)
    //        .Take(Math.Clamp(take, 1, 200))
    //        .ToListAsync();

    //    // Map to AccountSummaryDto (adjust to your real mapping)
    //    var result = new List<AccountSummaryDto>();
   
    //    return result;
    //}


    public async Task<IReadOnlyList<AccountSummaryDto>> ListByAccountclosed(int skip, int take)
    {
        var accts = await _db.Accounts
            .Where(a=> a.Status == AccountStatus.CLOSED && a.TenantId ==_ctx.TenantId  && a.BranchId == _ctx.BranchId)
            .OrderByDescending(a => a.AccountId).Skip(skip).Take(Math.Clamp(take, 1, 999999999))
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
            a.TotalAmount, a.Remarks ,a.SchemeId,a.BranchId,a.IsBidding ,a.CustomerCode ,a.ClosedDate ,a.Tenure);
    }

    //public async Task<AccountClosure> ClosedAccountAsync(AccountClosureRequest request)
    //{
    //    var closure = new AccountClosure 
    //    {
    //        ClosureId = request.ClosureId,
    //        TenantId = request.TenantId,
    //        BranchId = request.BranchId,
    //        MemberId = request.MemberId,
    //        AccountId = request.AccountId,
    //        AccountNumber = request.AccountNumber,
    //        CustomerName = request.CustomerName,
    //        PhoneNo = request.PhoneNo,
    //        InstallmentAmount = request.InstallmentAmount,
    //        TargetAmount = request.TargetAmount,
    //        BonusAmount = request.BonusAmount,
    //        LoanAmount = request.LoanAmount,
    //        ClosureDate = request.ClosureDate,
    //        Duration = request.Duration,
    //        InterestAmount = request.InterestAmount,
    //        ServFeeRate = request.ServFeeRate,
    //        ServiceFee = request.ServiceFee,
    //        AmountPayable = request.AmountPayable,
    //        PaymentMode = request.PaymentMode,
    //        GlName = request.GlName,
    //        VoucherNo = request.VoucherNo,
    //        Remarks = request.Remarks,
    //        Status = AccountStatus.CLOSED,
    //        ClosedAt = DateTime.UtcNow,
    //        CreatedBy = _ctx.UserId,
    //        CreatedAt = DateTime.UtcNow,
    //        UpdatedBy = request.UpdatedBy,
    //        Durations = request.Durations,
    //        UpdatedAt = DateTime.UtcNow,
    //        AuthorizedBy = request.AuthorizedBy,
    //        AuthorizedAt = request.AuthorizedAt
    //    };

    //    _db.AccountClosure.Add(closure);
    //    await _db.SaveChangesAsync();

    //    var account = await _db.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.AccountId == request.AccountId);

    //    if (account == null)
    //    {
    //        throw new Exception($"Account {request.AccountId} not found.");
    //    }
    //    var updatedAccount = new Account
    //    {
    //        AccountId = account.AccountId,
    //        AccountNumber = account.AccountNumber,
    //        MemberId = account.MemberId,
    //        TenantId = account.TenantId,
    //        MonthlyContribution = account.MonthlyContribution,
    //        AccountOpenDate = account.AccountOpenDate,
    //        TenureEndDate = account.TenureEndDate,
    //        Status = AccountStatus.CLOSED,
    //        InstallmentsPaid = account.InstallmentsPaid,
    //        IsPrized = account.IsPrized,
    //        CreatedAt = account.CreatedAt,
    //        OldAccountNo = account.OldAccountNo,
    //        PhoneNo = account.PhoneNo,
    //        CustomerName = account.CustomerName,
    //        InterestRate = account.InterestRate,
    //        TargetAmount = account.TargetAmount,
    //        PaymentDate = account.PaymentDate,
    //        PaidAmount = account.PaidAmount,
    //        LoanAmount = account.LoanAmount,
    //        BonusAmount = account.BonusAmount,
    //        InterestAmount = account.InterestAmount,
    //        TotalAmount = account.TotalAmount,
    //        Remarks = account.Remarks,
    //        SchemeId = account.SchemeId,
    //        BranchId = account.BranchId,
    //        IsBidding = account.IsBidding,
    //        CustomerCode = account.CustomerCode

    //    };

    //    _db.Accounts.Update(updatedAccount);
    //    await _db.SaveChangesAsync();

    //    //var cashCode = GetGLCodeByPaymentMode(closure.PaymentMode, closure.TenantId);

    //    // Store method-specific details

    //    //var journalId = await _accounting.PostJournalAsync( tenantId: closure.TenantId, entryDate:closure.ClosureDate , sourceType: JournalSourceType.LOAN_REPAYMENT,
    //    //sourceId: closure.AccountId, paymentMethod: closure.PaymentMode, description: $"Contribution from {closure.AccountNumber} via {closure.PaymentMode}",
    //    //lines: new[]
    //    //{
    //    //        new JournalLineInput(EntryTarget.GL, cashCode, null, closure.AmountPayable, 0),
    //    //        new JournalLineInput(EntryTarget.MEMBER_ACCOUNT, null, closure.AccountId, 0, closure.AmountPayable)
    //    //},
    //    //createdBy: _ctx.UserId,
    //    //authorizedBy: _ctx.UserId);


    //    _log.LogInformation("Closed account {Aid} {Num} for member {Mid} (tenant {Tid})",
    //        closure.AccountId, closure.AccountNumber, closure.MemberId, closure.TenantId);
    //    return closure;
    //}


    //private string GetGLCodeByPaymentMode(PaymentMethod paymentMode, int tenantId)
    //{
    //    // Get the GL account code based on payment mode
    //    var glCode = paymentMode switch
    //    {
    //        PaymentMethod.CASH => GetGLAccountCode("CASH", tenantId),
    //        PaymentMethod.BANK_TRANSFER => GetGLAccountCode("BANK", tenantId),
    //        PaymentMethod.CHEQUE => GetGLAccountCode("CHEQUE", tenantId),
    //        PaymentMethod.NEFT => GetGLAccountCode("NEFT", tenantId),
    //        PaymentMethod.RTGS => GetGLAccountCode("RTGS", tenantId),
    //        PaymentMethod.UPI => GetGLAccountCode("UPI", tenantId),
    //        PaymentMethod.EFT => GetGLAccountCode("EFT", tenantId),
    //        PaymentMethod.SYSTEM => throw new DomainException("SYSTEM payment method invalid for account closure"),
    //        _ => throw new DomainException($"Unknown payment method {paymentMode}")
    //    };

    //    if (string.IsNullOrEmpty(glCode))
    //    {
    //        throw new DomainException($"GL Code not found for payment method {paymentMode}");
    //    }

    //    return glCode;
    //}

    //private string GetGLAccountCode(string glName)
    //{
    //    // Get GL account code from database
    //    var glAccount = _db.GeneralLedgerMaster 
    //        .FirstOrDefault(g => g.GlId == glName);

    //    if (glAccount == null)
    //    {
    //        // Try to get default GL account
    //        glAccount = _db.GeneralLedgerMaster
    //            .FirstOrDefault(g => g.GlName == "ACCOUNT_CLOSURE" && g.TenantId == tenantId);
    //    }

    //    return glAccount?.Code ?? glAccount?.AccountNumber ?? throw new DomainException($"GL Account not found for {glName}");
    //}


    public async Task<AccountClosure> ClosedAccountAsync(AccountClosureRequest request)
    {
        try
        {
            // 1. Get the account - it's already being tracked
            var account = await _db.Accounts .Where(u => u.BranchId == _ctx.BranchId && u.TenantId == _ctx.TenantId)
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.AccountId == request.AccountId);

            if (account == null)
            {
                throw new Exception($"Account {request.AccountId} not found.");
            }

            // 2. Check if account is already closed
            if (account.Status == AccountStatus.CLOSED)
            {
                throw new DomainException($"Account {account.AccountNumber} is already closed.");
            }



            // 4. Create closure record
            var closure = new AccountClosure
            {
                TenantId = request.TenantId,
                BranchId = request.BranchId,
                MemberId = request.MemberId,
                AccountId = request.AccountId,
                AccountNumber = request.AccountNumber ?? account.AccountNumber,
                CustomerName = request.CustomerName ?? account.CustomerName,
                PhoneNo = request.PhoneNo ?? account.PhoneNo,
                InstallmentAmount = request.InstallmentAmount,
                TargetAmount = request.TargetAmount ?? account.TargetAmount,
                BonusAmount = request.BonusAmount ?? account.BonusAmount,
                LoanAmount = request.LoanAmount ?? account.LoanAmount,
                ClosureDate = request.ClosureDate,
                Duration = request.Duration,
                InterestAmount = request.InterestAmount,
                ServFeeRate = request.ServFeeRate,
                ServiceFee = request.ServiceFee,
                AmountPayable = request.AmountPayable,
                PaymentMode = request.PaymentMode.ToString(),
                GlName = request.GlName,
                GlCode =request.GlCode,
                VoucherNo = request.VoucherNo,
                Remarks = request.Remarks,
                Status = AccountStatus.CLOSED,
                ClosedAt = DateTime.UtcNow,
                CreatedBy = _ctx.UserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedBy = _ctx.UserId,
                UpdatedAt = DateTime.UtcNow,
                AuthorizedBy = _ctx.UserId,
                AuthorizedAt = DateTime.UtcNow,
                Durations = request.Duration // Assuming Durations is the same as Duration
            };

            await _db.AccountClosure.AddAsync(closure);
            await _db.SaveChangesAsync();

            account.Status = AccountStatus.CLOSED; // This should work if your entity uses the enum
                                                   // OR if the entity expects string:
            var existingAccount = await _db.Accounts .Where(u => u.BranchId == _ctx.BranchId && u.TenantId == _ctx.TenantId)
              .IgnoreQueryFilters()
              .FirstOrDefaultAsync(a => a.AccountId == request.AccountId);

            if (existingAccount != null)
            {
                // Update account status to CLOSED
                existingAccount.Status = AccountStatus.CLOSED;
                existingAccount.ClosedDate = request.ClosureDate;
                existingAccount.UpdatedBy = _ctx.UserId;
                existingAccount.UpdatedAt = DateTime.UtcNow;

                _log.LogInformation($"Updated account {existingAccount.AccountId} status to CLOSED for member {existingAccount.MemberId}");
            }
            else
            {
                throw new DomainException($"Account {request.AccountId} not found for update");
            }

            await _db.SaveChangesAsync();
            var method = request.PaymentMode;

            switch (method)
            {
                case PaymentMethod.NEFT:
                case PaymentMethod.RTGS:
                case PaymentMethod.BANK_TRANSFER:
                      break;

                case PaymentMethod.CASH:
                    request.PaymentMode = PaymentMethod.CASH;
                    request.Remarks = string.IsNullOrEmpty(request.Remarks)
                        ? "Cash payment received"
                        : request.Remarks;
                    break;

                default:
                    throw new ArgumentException($"Unsupported payment method: {request.PaymentMode}");
            }

            var journalId = await _accounting.PostJournalAsync(
                tenantId: existingAccount.TenantId,
                entryDate: _ctx.CurrentDate,
                sourceType: JournalSourceType.CONTRIBUTION,

                sourceId: existingAccount.AccountId,
              paymentMethod: method,
                description: $"Account Closure from {request.AccountNumber} via {request.PaymentMode}",
                lines: new[]
                {
                        new JournalLineInput(EntryTarget.GL, request.GlCode, null, request.AmountPayable, 0),
                        new JournalLineInput(EntryTarget.MEMBER_ACCOUNT, null, request.AccountId, 0, request.AmountPayable)
                },
                createdBy: _ctx.UserId,
                authorizedBy: _ctx.UserId);

            // 7. Create journal entry if needed
            // var cashCode = GetGLCodeByPaymentMode(closure.PaymentMode, closure.TenantId);
            // var journalId = await _accounting.PostJournalAsync(...);

            _log.LogInformation("Closed account {AccountId} {AccountNumber} for member {MemberId} (tenant {TenantId}, Amount: {AmountPayable:C})",
                closure.AccountId, closure.AccountNumber, closure.MemberId, closure.TenantId, closure.AmountPayable);

            return closure;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error closing account {AccountId}", request.AccountId);
            throw;
        }
    }


    public async Task<Account> UpdatePeriodChangeAsync(long accountId, PeriodChangeRequest request)
    {
        try
        {
            var account = await _db.Accounts
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.AccountId == accountId && a.TenantId == _ctx.TenantId && a.BranchId ==_ctx.BranchId);

            if (account == null)
            {
                throw new DomainException($"Account {accountId} not found");
            }

            if (account.Status == AccountStatus.CLOSED)
            {
                throw new DomainException($"The closed account {account.AccountNumber} cannot be updated.");
            }

            if (request.Period <= 0)
            {
                throw new DomainException("Period must be greater than 0");
            }

            if (request.MonthlyContribution <= 0)
            {
                throw new DomainException("Monthly contribution must be greater than 0");
            }

            var calculatedTarget = request.MonthlyContribution * request.Period;
            if (request.TargetAmount != calculatedTarget)
            {
                throw new DomainException($"Target amount must be {calculatedTarget} (Monthly Amount × Period)");
            }

            var oldValues = new
            {
                account.MonthlyContribution,
                account.TenureEndDate,
                account.TargetAmount
            };

            account.MonthlyContribution = request.MonthlyContribution;
            account.TargetAmount = request.TargetAmount;
            account.TenureEndDate = account.AccountOpenDate.AddMonths(request.Period);
            account.UpdatedAt = DateTime.UtcNow;
            account.UpdatedBy = _ctx.UserId;

            await _db.SaveChangesAsync();
            return account;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to update period for account {AccountId}", accountId);
            throw;
        }
    }

    public async Task<SifinCommission> SifinAccountAsync(SifinCommission request)
    {
        try
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.TenantId <= 0)
                throw new ArgumentException("TenantId is required");

            if (string.IsNullOrEmpty(request.GlCode))
                throw new ArgumentException("GL Code is required");

            if (request.CommissionAmount <= 0)
                throw new ArgumentException("Commission amount must be greater than 0");

            //if (string.IsNullOrEmpty(request.VocherNo))
            //    throw new ArgumentException("Voucher number is required");

            request.Remark = string.IsNullOrEmpty(request.Remark)
                ? $"Sifin Commission from {request.TenantId} via {request.PaymentMode}"
                : request.Remark;
            //request.VocherNo =_ctx.TenantId+""+_ctx.BranchId+""+DateTime.Now;
            var now = DateTime.Now;
            request.VocherNo = $"{_ctx.TenantId}{_ctx.BranchId}{now:yyyyMMdd}{now:HHmmss}";

            var commissionIncomeGl = await _db.GeneralLedgerMaster .Where(g => g.Code == "2003").Select(g => g.Code).FirstOrDefaultAsync();

            var method = request.PaymentMode;
            switch (method)
            {
                case PaymentMethod.NEFT:
                case PaymentMethod.RTGS:
                case PaymentMethod.BANK_TRANSFER:
                    break;

                case PaymentMethod.CASH:
                    break;

                default:
                    throw new ArgumentException($"Unsupported payment method: {request.PaymentMode}");
            }

            await _db.SifinCommission.AddAsync(request);
            await _db.SaveChangesAsync();

            var journalId = await _accounting.PostJournalAsync(
                tenantId: (long)request.TenantId,
                entryDate: _ctx.CurrentDate,
                sourceType: JournalSourceType.CONTRIBUTION,
                sourceId: request.TenantId,
                paymentMethod: method,
                description: request.Remark,
                lines: new[]  { new JournalLineInput(EntryTarget.GL,request.GlCode,null,request.CommissionAmount, 0 ),
                //new JournalLineInput( EntryTarget.GL, null,  request.TenantId,  0, request.CommissionAmount )
                 new JournalLineInput(EntryTarget.GL, commissionIncomeGl, null, 0, request.CommissionAmount)

                },
                createdBy: _ctx.UserId,
                authorizedBy: _ctx.UserId); 

            await _db.SaveChangesAsync();

            _log.LogInformation(
                "Closed account for tenant {TenantId} with amount {Amount:C}, Journal ID: {JournalId}, Voucher: {VoucherNo}",
                request.TenantId,request.CommissionAmount,   request.VocherNo
            );

            return request;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error closing account for tenant {TenantId}", request?.TenantId);
            throw;
        }
    } 
     
    public async Task<SifinCommission> GetsifinSummaryAsync()
    {
        var a = await _db.SifinCommission.Where(a => a.TenantId == _ctx.TenantId   && a.BranchId == _ctx.BranchId) 
            .IgnoreQueryFilters().FirstOrDefaultAsync(x => x.TenantId == _ctx.TenantId)
            ?? throw new DomainException($"Account {_ctx.TenantId} not found");
        return a;
    }


    public async Task<PoolMoneySummary> GetPoolMoneyAsync()
    {
        // Use ctx date, fall back to now if null
        var baseDate = (_ctx.CurrentDate != default
            ? _ctx.CurrentDate
            : DateOnly.FromDateTime(DateTime.UtcNow));

        var today = baseDate;
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        // Convert DateOnly to DateTime for provider-agnostic comparison
        var monthStartDt = monthStart.ToDateTime(TimeOnly.MinValue);
        var monthEndDt = monthEnd.ToDateTime(TimeOnly.MinValue);

        _log.LogInformation(
            "GetPoolMoneyAsync: tenant={Tenant}, branch={Branch}, from={From}, to={To}",
            _ctx.TenantId, _ctx.BranchId, monthStartDt, monthEndDt);

        var totals = await _db.LedgerEntries .Where(a => a.TenantId == _ctx.TenantId  && a.BranchId == _ctx.BranchId)
            .AsNoTracking()
            .Where(x => x.TenantId == _ctx.TenantId
                     && x.BranchId == _ctx.BranchId               
                     && x.EntryDate.ToDateTime(TimeOnly.MinValue) >= monthStartDt // Convert EntryDate to DateTime
                     && x.EntryDate.ToDateTime(TimeOnly.MinValue) < monthEndDt)  // Convert EntryDate to DateTime
            .GroupBy(x => x.EntryType)
            .Select(g => new
            {
                EntryType = g.Key,
                Total = g.Sum(x => x.Amount)  
            })
            .ToListAsync();

        var poolMoney = totals
            .FirstOrDefault(t => t.EntryType == LedgerEntryType.PAYMENT_RECEIVED)?.Total ?? 0m;

        var targetAmount = totals
            .FirstOrDefault(t => t.EntryType == LedgerEntryType.CONTRIBUTION_DUE)?.Total ?? 0m;

        _log.LogInformation(
            "GetPoolMoneyAsync: poolMoney={Pool}, target={Target}",
            poolMoney, targetAmount);

        return new PoolMoneySummary(poolMoney, targetAmount, monthStart, monthEnd);
    }
    public async Task<IReadOnlyList<AccountDto>> ListledgerEntrydata(int skip, int take)
    {
        // 🔹 Login working date → derive the calendar month
        var loginDate = _ctx.PreviousDate;                                  // 2026-08-28
        var monthStart = new DateOnly(loginDate.Year, loginDate.Month, 1);   // 2026-08-01
        var nextMonthStart = monthStart.AddMonths(1);                            // 2026-09-01

        var monthStartDt = monthStart.ToDateTime(TimeOnly.MinValue);             // 2026-08-01 00:00
        var monthEndDt = nextMonthStart.ToDateTime(TimeOnly.MinValue);         // 2026-09-01 00:00 (exclusive)

        // 🔹 Join ledger → account, group by account so each account appears once
        var rows = await (
            from l in _db.LedgerEntries.AsNoTracking()
            join a in _db.Accounts.AsNoTracking() on l.AccountId equals a.AccountId
            where l.TenantId == _ctx.TenantId
               && l.BranchId == _ctx.BranchId
               && l.EntryDate.ToDateTime(TimeOnly.MinValue) >= monthStartDt
               && l.EntryDate.ToDateTime(TimeOnly.MinValue) < monthEndDt
               && l.EntryType == LedgerEntryType.PAYMENT_RECEIVED
            group new { l, a } by new
            {
                a.AccountId,
                a.AccountNumber,
                a.MemberId,
                a.TenantId,
                a.MonthlyContribution,
                a.AccountOpenDate,
                a.TenureEndDate,
                a.Status,
                a.InstallmentsPaid,
                a.IsPrized,
                a.CreatedAt,
                a.OldAccountNo,
                a.PhoneNo,
                a.CustomerName,
                a.InterestRate,
                a.TargetAmount,
                a.LoanAmount,
                a.BonusAmount,
                a.InterestAmount,
                a.TotalAmount,
                a.Remarks,
                a.SchemeId,
                a.BranchId,
                a.IsBidding,
                a.CustomerCode,
                a.ClosedDate,
                a.Tenure,
                // 🔹 Added fields
                //a.IsEligibleToBid,
                //a.IsEligibleForDividend,
                //a.CorpusBalance,
                //a.PrizeWonInCycleId
            }
            into g
            orderby g.Key.AccountId
            select new
            {
                g.Key,
                PaymentDate = g.Max(x => x.l.EntryDate),   // latest payment in month
                PaidAmount = g.Sum(x => x.l.Amount)       // total paid in month
            })
            .Skip(skip)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync();

        // 🔹 Project to AccountSummaryDto
        var result = rows.Select(r => new AccountDto(
            AccountId: r.Key.AccountId,
            AccountNumber: r.Key.AccountNumber,
            MemberId: r.Key.MemberId,
            TenantId: r.Key.TenantId,
            MonthlyContribution: r.Key.MonthlyContribution,
            AccountOpenDate: r.Key.AccountOpenDate,
            TenureEndDate: r.Key.TenureEndDate,
            Status: r.Key.Status.ToString(),
            InstallmentsPaid: r.Key.InstallmentsPaid,
            IsPrized: r.Key.IsPrized,
            //IsEligibleToBid: r.Key.IsEligibleToBid,
            //IsEligibleForDividend: r.Key.IsEligibleForDividend,
            //CorpusBalance: r.Key.CorpusBalance,
            //PrizeWonInCycleId: r.Key.PrizeWonInCycleId,
            CreatedAt: r.Key.CreatedAt,
            OldAccountNo: r.Key.OldAccountNo ?? string.Empty,
            PhoneNo: r.Key.PhoneNo ?? string.Empty,
            CustomerName: r.Key.CustomerName ?? string.Empty,
            InterestRate: r.Key.InterestRate,
            TargetAmount: r.Key.TargetAmount,
            PaymentDate: r.PaymentDate,
            PaidAmount: r.PaidAmount,
            LoanAmount: r.Key.LoanAmount,
            //BonusAmount: r.Key.BonusAmount,
            InterestAmount: r.Key.InterestAmount,
            TotalAmount: r.Key.TotalAmount,
            Remarks: r.Key.Remarks ?? string.Empty,
            SchemeId: r.Key.SchemeId,
            BranchId: r.Key.BranchId,
            IsBidding: r.Key.IsBidding,
            //CustomerCode: r.Key.CustomerCode,
            ClosedDate: r.Key.ClosedDate,
            Tenure: r.Key.Tenure
        )).ToList(); 

        return result;
    }

    public async Task<(bool isSuccess, int rowCount, string errorCsv)> InsertvalidLedgerData(IFormFile file, string fileExtension, string batchCode)
    {
        var modifiedRows = new List<string>();
        var rowCount = 0;

        try
        {
            using var stream = file.OpenReadStream();
            string[] columns;
            List<string[]> rows;

            if (fileExtension == ".csv")
                (columns, rows) = ParseCsv(stream);
            else if (fileExtension == ".xlsx")
                (columns, rows) = ParseExcel(stream);
            else
                throw new Exception("Unsupported file format.");

            // Remove blank rows, trim values
            rows = rows
                .Where(r => r.Any(v => !string.IsNullOrWhiteSpace(v)))
                .Select(r => r.Select(v => v?.Trim() ?? "").ToArray())
                .ToList();

            rowCount = rows.Count;

            // Bulk insert into staging table
            var (inserted, error) = InsertRowsToStaging(rows, columns, batchCode);
            if (!inserted) return (false, rowCount, error);

            // Call MySQL validation function
            var (isValid, errCsv) = CallKycValidationFunction();
            if (!isValid)
                return (false, rowCount, errCsv);

            return (true, rowCount, null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error processing KYC file");
            throw;
        }
    }
    private (bool, string) CallKycValidationFunction()
    {
        return (true, null);
    }

    private (string[] columns, List<string[]> rows) ParseCsv(Stream fileStream)
    {
        var rows = new List<string[]>();
        string[] columns;

        using (var reader = new StreamReader(fileStream))
        {
            var headerLine = reader.ReadLine();
            if (headerLine == null)
                throw new Exception("CSV file is empty.");

            columns = headerLine.Split(',');

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                rows.Add(line.Split(','));
            }
        }

        return (columns, rows);
    }




    // Resolve the ambiguity by using fully qualified names for ExcelPackage
    private (string[] columns, List<string[]> rows) ParseExcel(Stream fileStream)
    {
        var rows = new List<string[]>();
        ExcelPackage.License.SetNonCommercialPersonal("TechVerve");


        using (var package = new ExcelPackage(fileStream)) // Fully qualified name
        {
            var worksheet = package.Workbook.Worksheets[0];
            var rowCount = worksheet.Dimension.Rows;
            var colCount = worksheet.Dimension.Columns;

            var columns = new string[colCount];
            for (int col = 1; col <= colCount; col++)
            {
                columns[col - 1] = worksheet.Cells[1, col].Text;
            }

            for (int row = 2; row <= rowCount; row++)
            {
                var values = new string[colCount];
                for (int col = 1; col <= colCount; col++)
                {
                    values[col - 1] = worksheet.Cells[row, col].Text;
                }
                rows.Add(values);
            }

            return (columns, rows);
        }
    }

    private (bool, string) InsertRowsToStaging(List<string[]> rows, string[] columns, string batchCode)
    {
        string connectionString = _configuration.GetConnectionString("Default");

        using var connection = new MySqlConnection(connectionString);
        connection.Open();
        using var tx = connection.BeginTransaction();

        try
        {

            var colNames = string.Join(", ", columns.Select(c => $"`{c}`")) + ", `batch_code`";
            var paramPlaceholders = string.Join(", ",
                Enumerable.Range(0, columns.Length + 1).Select(i => $"@p{i}"));

            var insertSql = $"INSERT INTO temp_ledger_entry ({colNames}) VALUES ({paramPlaceholders})";

            //foreach (var row in rows)
            //{
            //    using var cmd = new MySqlCommand(insertSql, connection, tx);
            //    for (int i = 0; i < columns.Length; i++)
            //    {
            //        cmd.Parameters.AddWithValue($"@p{i}",
            //            i < row.Length ? (object?)row[i] ?? DBNull.Value : DBNull.Value);
            //    }
            //    cmd.Parameters.AddWithValue($"@p{columns.Length}", batchCode);
            //    cmd.ExecuteNonQuery();
            //}

            foreach (var row in rows)
            {
                using var cmd = new MySqlCommand(insertSql, connection, tx);

                for (int i = 0; i < columns.Length; i++)
                {
                    object value = DBNull.Value;

                    if (i < row.Length && row[i] != null)
                    {
                        string? s = row[i]?.ToString()?.Trim();

                        // treat these as NULL
                        bool isNullLike =
                            string.IsNullOrEmpty(s) ||
                            s.Equals("NULL", StringComparison.OrdinalIgnoreCase) ||
                            s.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                            s.Equals("NA", StringComparison.OrdinalIgnoreCase) ||
                            s.Equals("NULL", StringComparison.OrdinalIgnoreCase) ||
                            s == "-" ||
                            s == "\\N";   // MySQL's own NULL marker in TSV files

                        if (!isNullLike)
                        {
                            value = row[i];   // keep original type for numeric columns
                        }
                    }

                    cmd.Parameters.AddWithValue($"@p{i}", value);
                }

                cmd.Parameters.AddWithValue($"@p{columns.Length}", batchCode);
                cmd.ExecuteNonQuery();
            }

            long? bankId = _ctx.TenantId;
            long? branchId = _ctx.BranchId;
            using (var cmd = new MySqlCommand("CALL upload_ledger_data(@bankId,@branchId)", connection, tx))
            {
                cmd.Parameters.AddWithValue("@bankId", bankId);
                cmd.Parameters.AddWithValue("@branchId", branchId);

                cmd.ExecuteNonQuery();
            }

            int errorCount;
            using (var cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM temp_ledger_entry WHERE error = 1", connection, tx))
            {
                errorCount = Convert.ToInt32(cmd.ExecuteScalar());
            }

            if (errorCount > 0)
            {
                var output = new List<string>();
                using (var errCmd = new MySqlCommand(
                    "SELECT * FROM temp_ledger_entry WHERE error = 1", connection, tx))
                using (var reader = errCmd.ExecuteReader())
                {
                    var include = new List<int>();
                    var headerNames = new List<string>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var name = reader.GetName(i);
                        if (name is "batch_code" or "error") continue;
                        include.Add(i);
                        headerNames.Add(name);
                    }
                    output.Add(string.Join(",", headerNames));

                    while (reader.Read())
                    {
                        var vals = include
                            .Select(i => reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString())
                            .Select(v => v!.Replace(",", " ")); // avoid breaking CSV columns
                        output.Add(string.Join(",", vals));
                    }
                } // ← reader disposed here, connection free again

                tx.Rollback();
                return (false, string.Join(Environment.NewLine, output));
            }

            tx.Commit();
            return (true, null!);
        }
        catch (MySqlException ex)
        {
            try { tx.Rollback(); } catch { /* ignore */ }
            _log.LogError(ex, "MySQL error in InsertRowsToStaging"); 
            return (false, ex.Message);
        }
    }

    public async Task<(bool isSuccess, int rowCount, string errorCsv)> InsertvalidAccData(IFormFile file, string fileExtension, string batchCode)
    {
        var modifiedRows = new List<string>();
        var rowCount = 0;

        try
        {
            using var stream = file.OpenReadStream();
            string[] columns;
            List<string[]> rows;

            if (fileExtension == ".csv")
                (columns, rows) = ParseCsv(stream);
            else if (fileExtension == ".xlsx")
                (columns, rows) = ParseExcel(stream);
            else
                throw new Exception("Unsupported file format.");

            // Remove blank rows, trim values
            rows = rows
                .Where(r => r.Any(v => !string.IsNullOrWhiteSpace(v)))
                .Select(r => r.Select(v => v?.Trim() ?? "").ToArray())
                .ToList();

            rowCount = rows.Count;

            // Bulk insert into staging table
            var (inserted, error) = InsertAccountData(rows, columns, batchCode);
            if (!inserted) return (false, rowCount, error);

            // Call MySQL validation function
            var (isValid, errCsv) = CallKycValidationFunction();
            if (!isValid)
                return (false, rowCount, errCsv);

            return (true, rowCount, null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error processing KYC file");
            throw;
        }
    }
    private (bool, string) InsertAccountData(
  List<string[]> rows, string[] columns, string batchCode)
    {
        string connectionString = _configuration.GetConnectionString("Default");

        using var connection = new MySqlConnection(connectionString);
        connection.Open();
        using var tx = connection.BeginTransaction();

        try
        {

            var colNames = string.Join(", ", columns.Select(c => $"`{c}`")) + ", `batch_code`";
            var paramPlaceholders = string.Join(", ",
                Enumerable.Range(0, columns.Length + 1).Select(i => $"@p{i}"));

            var insertSql =
                $"INSERT INTO temp_account({colNames}) VALUES ({paramPlaceholders})";

            foreach (var row in rows)
            {
                using var cmd = new MySqlCommand(insertSql, connection, tx);

                for (int i = 0; i < columns.Length; i++)
                {
                    object value = DBNull.Value;

                    if (i < row.Length && row[i] != null)
                    {
                        string? s = row[i]?.ToString()?.Trim();

                        // treat these as NULL
                        bool isNullLike =
                            string.IsNullOrEmpty(s) ||
                            s.Equals("NULL", StringComparison.OrdinalIgnoreCase) ||
                            s.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                            s.Equals("NA", StringComparison.OrdinalIgnoreCase) ||
                            s.Equals("NULL", StringComparison.OrdinalIgnoreCase) ||
                            s == "-" ||
                            s == "\\N";   // MySQL's own NULL marker in TSV files

                        if (!isNullLike)
                        {
                            value = row[i];   // keep original type for numeric columns
                        }
                    }

                    cmd.Parameters.AddWithValue($"@p{i}", value);
                }

                cmd.Parameters.AddWithValue($"@p{columns.Length}", batchCode);
                cmd.ExecuteNonQuery();
            }
            long? bankId = _ctx.TenantId;
            long? branchId = _ctx.BranchId;
            using (var cmd = new MySqlCommand("CALL validate_upload_account(@bankId,@branchId)", connection, tx))
            {
                cmd.Parameters.AddWithValue("@bankId", bankId);
                cmd.Parameters.AddWithValue("@branchId", branchId);

                cmd.ExecuteNonQuery();
            }

            int errorCount;
            using (var cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM temp_account WHERE error = 1", connection, tx))
            {
                errorCount = Convert.ToInt32(cmd.ExecuteScalar());
            }

            if (errorCount > 0)
            {
                var output = new List<string>();
                using (var errCmd = new MySqlCommand(
                    "SELECT * FROM temp_account WHERE error = 1", connection, tx))
                using (var reader = errCmd.ExecuteReader())
                {
                    var include = new List<int>();
                    var headerNames = new List<string>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var name = reader.GetName(i);
                        if (name is "batch_code" or "error") continue;
                        include.Add(i);
                        headerNames.Add(name);
                    }
                    output.Add(string.Join(",", headerNames));

                    while (reader.Read())
                    {
                        var vals = include
                            .Select(i => reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString())
                            .Select(v => v!.Replace(",", " ")); // avoid breaking CSV columns
                        output.Add(string.Join(",", vals));
                    }
                } // ← reader disposed here, connection free again

                tx.Rollback();
                return (false, string.Join(Environment.NewLine, output));
            }

            tx.Commit();
            return (true, null!);
        }
        catch (MySqlException ex)
        {
            try { tx.Rollback(); } catch { /* ignore */ }
            _log.LogError(ex, "MySQL error in InsertRowsToStaging");
            return (false, ex.Message);
        }
    }

    public async Task<(bool isSuccess, string errorMessage)> InsertToDatabase(string batchCode)
    {
        string connectionString = _configuration.GetConnectionString("Default");
        var connection = new MySqlConnection(connectionString);
        connection.Open();

        try
        {
            long? tenantId = _ctx.TenantId;
            long? branchId = _ctx.BranchId;

            using (var cmd = new MySqlCommand("CALL upload_temp_account_data(@tenantId, @branchId)", connection))
            {
                cmd.Parameters.AddWithValue("@tenantId", tenantId);
                cmd.Parameters.AddWithValue("@branchId", branchId);
                cmd.ExecuteNonQuery();
            }


            return (true, null);
        }

        catch (Exception ex)
        {
            _log.LogError(ex, "Error in InsertToDatabase");
            return (false, $"Unexpected error: {ex.Message}");
        }
    }



    public async Task<(bool isSuccess, int rowCount, string errorCsv)> InsertvalidApaymentData(IFormFile file, string fileExtension, string batchCode)
    {
        var modifiedRows = new List<string>();
        var rowCount = 0;

        try
        {
            using var stream = file.OpenReadStream();
            string[] columns;
            List<string[]> rows;

            if (fileExtension == ".csv")
                (columns, rows) = ParseCsv(stream);
            else if (fileExtension == ".xlsx")
                (columns, rows) = ParseExcel(stream);
            else
                throw new Exception("Unsupported file format.");

            // Remove blank rows, trim values
            rows = rows
                .Where(r => r.Any(v => !string.IsNullOrWhiteSpace(v)))
                .Select(r => r.Select(v => v?.Trim() ?? "").ToArray())
                .ToList();

            rowCount = rows.Count;

            // Bulk insert into staging table
            var (inserted, error) = InsertpaymentData(rows, columns, batchCode);
            if (!inserted) return (false, rowCount, error);

            // Call MySQL validation function
            var (isValid, errCsv) = CallKycValidationFunction();
            if (!isValid)
                return (false, rowCount, errCsv);

            return (true, rowCount, null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error processing KYC file");
            throw;
        }
    }
    private (bool, string) InsertpaymentData(
  List<string[]> rows, string[] columns, string batchCode)
    {
        string connectionString = _configuration.GetConnectionString("Default");

        using var connection = new MySqlConnection(connectionString);
        connection.Open();
        using var tx = connection.BeginTransaction();

        try
        {

            var colNames = string.Join(", ", columns.Select(c => $"`{c}`")) + ", `batch_code`";
            var paramPlaceholders = string.Join(", ",
                Enumerable.Range(0, columns.Length + 1).Select(i => $"@p{i}"));

            var del = "DELETE FROM temp_payment_detail";

            var insertSql = $"INSERT INTO temp_payment_detail({colNames}) VALUES ({paramPlaceholders})";

            foreach (var row in rows)
            {
                using var cmd = new MySqlCommand(insertSql, connection, tx);

                for (int i = 0; i < columns.Length; i++)
                {
                    object value = DBNull.Value;

                    if (i < row.Length && row[i] != null)
                    {
                        string? s = row[i]?.ToString()?.Trim();

                        // treat these as NULL
                        bool isNullLike =
                            string.IsNullOrEmpty(s) ||
                            s.Equals("NULL", StringComparison.OrdinalIgnoreCase) ||
                            s.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                            s.Equals("NA", StringComparison.OrdinalIgnoreCase) ||
                            s.Equals("NULL", StringComparison.OrdinalIgnoreCase) ||
                            s == "-" ||
                            s == "\\N";   // MySQL's own NULL marker in TSV files

                        if (!isNullLike)
                        {
                            value = row[i];   // keep original type for numeric columns
                        }
                    }

                    cmd.Parameters.AddWithValue($"@p{i}", value);
                }

                cmd.Parameters.AddWithValue($"@p{columns.Length}", batchCode);
                cmd.ExecuteNonQuery();
            }
           long? bankId = _ctx.TenantId;
            long? branchId = _ctx.BranchId;
            using (var cmd = new MySqlCommand("CALL validate_upload_payment(@bankId,@branchId)", connection, tx))
            {
                cmd.Parameters.AddWithValue("@bankId", bankId);
                cmd.Parameters.AddWithValue("@branchId", branchId);
                cmd.CommandTimeout = 300; // 5 minutes

                cmd.ExecuteNonQuery();
            }

            int errorCount;
            using (var cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM temp_payment_detail WHERE error = 1", connection, tx))
            {
                errorCount = Convert.ToInt32(cmd.ExecuteScalar());
            }

            if (errorCount > 0)
            {
                var output = new List<string>();
                using (var errCmd = new MySqlCommand(
                    "SELECT * FROM temp_payment_detail WHERE error = 1", connection, tx))
                using (var reader = errCmd.ExecuteReader())
                {
                    var include = new List<int>();
                    var headerNames = new List<string>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var name = reader.GetName(i);
                        if (name is "batch_code" or "error") continue;
                        include.Add(i);
                        headerNames.Add(name);
                    }
                    output.Add(string.Join(",", headerNames));

                    while (reader.Read())
                    {
                        var vals = include
                            .Select(i => reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString())
                            .Select(v => v!.Replace(",", " ")); // avoid breaking CSV columns
                        output.Add(string.Join(",", vals));
                    }
                } // ← reader disposed here, connection free again

                tx.Rollback();
                return (false, string.Join(Environment.NewLine, output));
            }

            tx.Commit();
            return (true, null!);
        }
        catch (MySqlException ex)
        {
            try { tx.Rollback(); } catch { /* ignore */ }
            _log.LogError(ex, "MySQL error in InsertRowsToStaging");
            return (false, ex.Message);
        }
    }

    public async Task<(bool isSuccess, string errorMessage)> InsertToPaymentDatabase(string batchCode)
    {
        string connectionString = _configuration.GetConnectionString("Default");
        var connection = new MySqlConnection(connectionString); 
        connection.Open();

        try
        {
            long? tenantId = _ctx.TenantId;
            long? branchId = _ctx.BranchId;

            using (var cmd = new MySqlCommand("CALL upload_temp_account_data(@tenantId, @branchId)", connection))
            {
                cmd.Parameters.AddWithValue("@tenantId", tenantId);
                cmd.Parameters.AddWithValue("@branchId", branchId);
                cmd.ExecuteNonQuery();
            }


            return (true, null);
        }

        catch (Exception ex)
        {
            _log.LogError(ex, "Error in InsertToDatabase");
            return (false, $"Unexpected error: {ex.Message}");
        }
    }


    public async Task<(bool isSuccess, string errorMessage)> InsertToLedgerDatabase(string batchCode)
    {
        string connectionString = _configuration.GetConnectionString("Default"); 
        var connection = new MySqlConnection(connectionString);
        connection.Open();

        try
        {
            long? tenantId = _ctx.TenantId;
            long? branchId = _ctx.BranchId;

            using (var cmd = new MySqlCommand("CALL upload_ledger_detail_data(@tenantId, @branchId)", connection))
            {
                cmd.Parameters.AddWithValue("@tenantId", tenantId);
                cmd.Parameters.AddWithValue("@branchId", branchId);
                cmd.ExecuteNonQuery();
            }


            return (true, null);
        }

        catch (Exception ex)
        {
            _log.LogError(ex, "Error in InsertToDatabase");
            return (false, $"Unexpected error: {ex.Message}");
        }
    }

}
