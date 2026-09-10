using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Reflection.Emit;

namespace EasyMoney.Api.Services;

public interface IAccountService
{
    Task<Account> OpenAccountAsync(long memberId, decimal monthlyContribution, DateOnly openDate, long? createdBy, long? authorizedBy, AccountOpenPayload payload);
    Task<Account> UpdateAccountAsync(long accountId, long memberId, decimal monthlyContribution, DateOnly openDate, long? updateBy, long? authorizedBy, AccountUpdatePayload payload);
     
    Task<AccountSummaryDto> GetSummaryAsync(long accountId); 
    Task<AccountSummaryDto> GetAccountAsync(long accountId); 
    Task<IReadOnlyList<AccountSummaryDto>> ListByMemberAsync(long memberId); 
    Task<IReadOnlyList<AccountSummaryDto>> ListByTenantAsync(int skip, int take);
    Task<IReadOnlyList<AccountSummaryDto>> ListByAccountclosed(int skip, int take); 


    /// <summary>Transition account.status to COMPLETED. Called by MonthlyCycleJob on tenure end.</summary>
    Task CompleteTenureAsync(long accountId);
    Task<Account> GetMemberAsync(int schemeId, long memberId); 

    Task<AccountClosure> ClosedAccountAsync(AccountClosureRequest payload);
    Task<SifinCommission> SifinAccountAsync(SifinCommission payload);
    Task<SifinCommission> GetsifinSummaryAsync();

    Task<Account> UpdatePeriodChangeAsync(long accountId, PeriodChangeRequest request);

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
            BranchId = member.BranchId,
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
            CustomerCode = req.CustomerCode,
            IsBidding = "N",
            FirstPaymentFlag = "N",

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

    public async Task<AccountSummaryDto> GetAccountAsync(long accountId)
    {
        var a = await _db.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.AccountId == accountId && x.TenantId==_ctx.TenantId)
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



    public async Task<IReadOnlyList<AccountSummaryDto>> ListByAccountclosed(int skip, int take)
    {
        var accts = await _db.Accounts
            .Where(a=> a.Status == AccountStatus.CLOSED && a.TenantId ==_ctx.TenantId)
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
            var account = await _db.Accounts
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
            var existingAccount = await _db.Accounts
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
                .FirstOrDefaultAsync(a => a.AccountId == accountId && a.TenantId == _ctx.TenantId);

            if (account == null)
            {
                throw new DomainException($"Account {accountId} not found");
            }

            if (account.Status == AccountStatus.CLOSED)
            {
                throw new DomainException($"Cannot update closed account {account.AccountNumber}");
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
        var a = await _db.SifinCommission.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.TenantId == _ctx.TenantId)
            ?? throw new DomainException($"Account {_ctx.TenantId} not found");
        return a;
    }
}
