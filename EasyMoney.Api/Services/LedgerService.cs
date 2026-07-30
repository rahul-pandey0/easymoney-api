using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EasyMoney.Api.Services;

public interface ILedgerService
{
    /// <summary>
    /// Record a contribution payment for one account. Posts the GL journal:
    ///   Dr CASH | BANK | EFT (per payment_method)
    ///   Cr MEMBER_ACCOUNT (subsidiary corpus)
    /// Also writes a ledger_entry (PAYMENT_RECEIVED) row linked to dueId (if supplied)
    /// and bumps installments_paid.
    /// </summary>
    //Task<PaymentResultDto> RecordPaymentAsync(long accountId, decimal amount, DateOnly paidDate, PaymentMethod method, long? dueId = null);
    //Task<PaymentResultDto> RecordPaymentAsync(long accountId, RecordPaymentRequest request, PaymentMethod method);

    Task<PaymentResultDto> RecordPaymentAsync(long accountId, decimal amount, DateOnly paidDate, PaymentMethod method, long? dueId = null);

    /// <summary>
    /// Called once when an account is opened. Writes all CONTRIBUTION_DUE entries upfront
    /// for every month of the tenure (tenureMonths rows). Idempotent — skips months already present.
    /// </summary>
    Task GenerateAllDuesForAccountAsync(long accountId);

    /// <summary>
    /// Returns due lines for months up to and including today.
    /// Each line carries its dueId so the caller can submit payment against a specific due.
    /// </summary>
    Task<IReadOnlyList<DueLineDto>> GetDuesAsync(long accountId);
}

public class LedgerService : ILedgerService
{
    private readonly EasyMoneyDbContext _db;
    private readonly ITenantContext _ctx;
    private readonly IAccountingService _accounting;
    private readonly ILogger<LedgerService> _log;

    public LedgerService(EasyMoneyDbContext db, ITenantContext ctx, IAccountingService accounting, ILogger<LedgerService> log)
    {
        _db = db; _ctx = ctx; _accounting = accounting; _log = log;
    }

    //public async Task<PaymentResultDto> RecordPaymentAsync(long accountId, decimal amount, DateOnly paidDate, PaymentMethod method, long? dueId = null)
    //public async Task<PaymentResultDto> RecordPaymentAsync(long accountId, RecordPaymentRequest request, PaymentMethod method)
    //{

    //    decimal amount = request.InstallmentAmount;
    //    DateOnly paidDate = request.PaidDate;
    //    long? dueId = request.DueId;
    //    decimal penalty = request.PenaltyAmount;
    //    decimal otherCharges = request.OtherCharges;
    //    decimal totalAmount = request.TotalAmount;

    //    if (amount <= 0) throw new DomainException("Amount must be > 0");

    //    if (totalAmount != amount + penalty + otherCharges) throw new DomainException("Total amount mismatch.");

    //    var a = await _db.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.AccountId == accountId)
    //        ?? throw new DomainException($"Account {accountId} not found");
    //    if (a.Status is not (AccountStatus.ACTIVE or AccountStatus.PRIZED))
    //        throw new DomainException($"Account is {a.Status}; cannot accept payment");

    //    // Validate the targeted due line belongs to this account and is not already paid
    //    if (dueId.HasValue)
    //    {
    //        var due = await _db.LedgerEntries.IgnoreQueryFilters()
    //            .FirstOrDefaultAsync(e => e.EntryId == dueId.Value)
    //            ?? throw new DomainException($"Due line {dueId} not found");
    //        if (due.AccountId != accountId)
    //            throw new DomainException($"Due line {dueId} does not belong to account {accountId}");
    //        if (due.EntryType != LedgerEntryType.CONTRIBUTION_DUE)
    //            throw new DomainException($"Entry {dueId} is not a CONTRIBUTION_DUE");

    //        // Check if already fully paid by looking for an existing linked payment
    //        var alreadyPaid = await _db.LedgerEntries.IgnoreQueryFilters()
    //            .AnyAsync(e => e.LinkedEntryId == dueId.Value && e.EntryType == LedgerEntryType.PAYMENT_RECEIVED);
    //        if (alreadyPaid)
    //            throw new DomainException($"Due line {dueId} already has a payment recorded");
    //    }

    //    var cashCode = method switch
    //    {
    //        PaymentMethod.CASH => SystemGl.CashInHand,
    //        PaymentMethod.BANK_TRANSFER or PaymentMethod.CHEQUE or PaymentMethod.NEFT or PaymentMethod.RTGS => SystemGl.Bank,
    //        PaymentMethod.EFT or PaymentMethod.UPI => SystemGl.EftClearing,
    //        PaymentMethod.SYSTEM => throw new DomainException("SYSTEM payment method invalid for member payments"),
    //        _ => throw new DomainException($"Unknown payment method {method}")
    //    };

    //    // Post the balanced GL journal:
    //    //   Dr cashCode         amount
    //    //   Cr MEMBER_ACCOUNT   amount (credit to member's corpus -> positive balance grows)
    //    var journalId = await _accounting.PostJournalAsync(
    //        tenantId: a.TenantId,
    //        entryDate: paidDate,
    //        sourceType: JournalSourceType.CONTRIBUTION,
    //        sourceId: a.AccountId,
    //        paymentMethod: method,
    //        description: $"Contribution from {a.AccountNumber} via {method}",
    //        lines: new[]
    //        {
    //            //new JournalLineInput(EntryTarget.GL, cashCode, null, amount, 0),
    //            //new JournalLineInput(EntryTarget.MEMBER_ACCOUNT, null, a.AccountId, 0, amount)
    //            new JournalLineInput(EntryTarget.GL, cashCode, null, totalAmount, 0),
    //            new JournalLineInput(EntryTarget.MEMBER_ACCOUNT, null, a.AccountId, 0, totalAmount)
    //        },
    //        createdBy: _ctx.UserId,
    //        authorizedBy: _ctx.UserId);

    //    // Write subsidiary ledger_entry (PAYMENT_RECEIVED), linked to the due line if supplied
    //    _db.LedgerEntries.Add(new LedgerEntry
    //    {
    //        TenantId = a.TenantId,
    //        AccountId = a.AccountId,
    //        CycleId = null,
    //        LinkedEntryId = dueId,
    //        EntryType = LedgerEntryType.PAYMENT_RECEIVED,
    //        Amount = totalAmount,
    //        EntryDate = paidDate,
    //        Description = $"Contribution payment via {method}",
    //        CreatedBy = _ctx.UserId,
    //        Remarks = request.Remarks,
    //        VoucherNo = request.VoucherNo,
    //        GlAccountId = request.GlAccountId,
    //        GlAccountName = request.GlAccountName,
    //        PhoneNumber = request.PhoneNumber,

    //    });

    //    // Increment installments_paid by floor(amount / monthly_contribution)
    //    int delta = (int)Math.Floor(amount / a.MonthlyContribution);
    //    if (delta > 0) a.InstallmentsPaid += delta;
    //    if (request.ClosurePayment)
    //    {
    //        a.Status = AccountStatus.COMPLETED;
    //    }
    //    await _db.SaveChangesAsync();

    //    var corpus = await _accounting.GetMemberAccountBalanceAsync(a.AccountId);
    //    _log.LogInformation("Payment {Amt} for account {Aid} (dueId={DueId}, installments={Inst}, corpus={Bal})",
    //        totalAmount, accountId, dueId, a.InstallmentsPaid, corpus);
    //    return new PaymentResultDto(a.AccountId, totalAmount, a.InstallmentsPaid, corpus, journalId);
    //}

    public async Task<PaymentResultDto> RecordPaymentAsync(long accountId, decimal amount, DateOnly paidDate, PaymentMethod method, long? dueId = null)
    {
        if (amount <= 0) throw new DomainException("Amount must be > 0");

        var a = await _db.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.AccountId == accountId)
            ?? throw new DomainException($"Account {accountId} not found");
        if (a.Status is not (AccountStatus.ACTIVE or AccountStatus.PRIZED))
            throw new DomainException($"Account is {a.Status}; cannot accept payment");

        // Validate the targeted due line belongs to this account and is not already paid
        if (dueId.HasValue)
        {
            var due = await _db.LedgerEntries.IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.EntryId == dueId.Value)
                ?? throw new DomainException($"Due line {dueId} not found");
            if (due.AccountId != accountId)
                throw new DomainException($"Due line {dueId} does not belong to account {accountId}");
            if (due.EntryType != LedgerEntryType.CONTRIBUTION_DUE)
                throw new DomainException($"Entry {dueId} is not a CONTRIBUTION_DUE");

            // Check if already fully paid by looking for an existing linked payment
            var alreadyPaid = await _db.LedgerEntries.IgnoreQueryFilters()
                .AnyAsync(e => e.LinkedEntryId == dueId.Value && e.EntryType == LedgerEntryType.PAYMENT_RECEIVED);
            if (alreadyPaid)
                throw new DomainException($"Due line {dueId} already has a payment recorded");
        }

        var cashCode = method switch
        {
            PaymentMethod.CASH => SystemGl.CashInHand,
            PaymentMethod.BANK_TRANSFER or PaymentMethod.CHEQUE or PaymentMethod.NEFT or PaymentMethod.RTGS => SystemGl.Bank,
            PaymentMethod.EFT or PaymentMethod.UPI => SystemGl.EftClearing,
            PaymentMethod.SYSTEM => throw new DomainException("SYSTEM payment method invalid for member payments"),
            _ => throw new DomainException($"Unknown payment method {method}")
        };

        // Post the balanced GL journal:
        //   Dr cashCode         amount
        //   Cr MEMBER_ACCOUNT   amount (credit to member's corpus -> positive balance grows)
        var journalId = await _accounting.PostJournalAsync(
            tenantId: a.TenantId,
            entryDate: paidDate,
            sourceType: JournalSourceType.CONTRIBUTION,
            sourceId: a.AccountId,
            paymentMethod: method,
            description: $"Contribution from {a.AccountNumber} via {method}",
            lines: new[]
            {
                new JournalLineInput(EntryTarget.GL, cashCode, null, amount, 0),
                new JournalLineInput(EntryTarget.MEMBER_ACCOUNT, null, a.AccountId, 0, amount)
            },
            createdBy: _ctx.UserId,
            authorizedBy: _ctx.UserId);

        // Write subsidiary ledger_entry (PAYMENT_RECEIVED), linked to the due line if supplied
        _db.LedgerEntries.Add(new LedgerEntry
        {
            TenantId = a.TenantId,
            AccountId = a.AccountId,
            CycleId = null,
            LinkedEntryId = dueId,
            EntryType = LedgerEntryType.PAYMENT_RECEIVED,
            Amount = amount,
            EntryDate = paidDate,
            Description = $"Contribution payment via {method}",
            CreatedBy = _ctx.UserId
        });

        // Increment installments_paid by floor(amount / monthly_contribution)
        int delta = (int)Math.Floor(amount / a.MonthlyContribution);
        if (delta > 0) a.InstallmentsPaid += delta;
        await _db.SaveChangesAsync();

        var corpus = await _accounting.GetMemberAccountBalanceAsync(a.AccountId);
        _log.LogInformation("Payment {Amt} for account {Aid} (dueId={DueId}, installments={Inst}, corpus={Bal})",
            amount, accountId, dueId, a.InstallmentsPaid, corpus);
        return new PaymentResultDto(a.AccountId, amount, a.InstallmentsPaid, corpus, journalId);
    }


    public async Task GenerateAllDuesForAccountAsync(long accountId)
    {
        var a = await _db.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.AccountId == accountId)
            ?? throw new DomainException($"Account {accountId} not found");

        var scheme = await _db.SchemeConfigs.IgnoreQueryFilters().FirstAsync(s => s.TenantId == a.TenantId);

        // Existing due dates — skip months already written (idempotent)
        var existing = await _db.LedgerEntries.IgnoreQueryFilters()
            .Where(l => l.AccountId == accountId && l.EntryType == LedgerEntryType.CONTRIBUTION_DUE)
            .Select(l => l.EntryDate)
            .ToListAsync();
        var existingSet = new HashSet<DateOnly>(existing);

        var entries = new List<LedgerEntry>();
        for (int i = 0; i < scheme.TenureMonths; i++)
        {
            var dueMonth = new DateOnly(a.AccountOpenDate.Year, a.AccountOpenDate.Month, 1).AddMonths(i);
            if (existingSet.Contains(dueMonth)) continue;
            entries.Add(new LedgerEntry
            {
                TenantId = a.TenantId,
                AccountId = a.AccountId,
                CycleId = null,
                EntryType = LedgerEntryType.CONTRIBUTION_DUE,
                Amount = a.MonthlyContribution,
                EntryDate = dueMonth,
                Description = $"Contribution due for {dueMonth:yyyy-MM}",
                CreatedBy = _ctx.UserId
            });
        }

        if (entries.Count > 0)
        {
            _db.LedgerEntries.AddRange(entries);
            await _db.SaveChangesAsync();
            _log.LogInformation("Generated {Count} CONTRIBUTION_DUE entries for account {Aid} (tenure {Months} months)",
                entries.Count, accountId, scheme.TenureMonths);
        }
    }

    public async Task<IReadOnlyList<DueLineDto>> GetDuesAsync(long accountId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Only show due months up to and including the current month
        var dues = await _db.LedgerEntries.IgnoreQueryFilters()
            .Where(l => l.AccountId == accountId
                        && l.EntryType == LedgerEntryType.CONTRIBUTION_DUE
                        && l.EntryDate <= today)
            .OrderBy(l => l.EntryDate)
            .Select(l => new { l.EntryId, l.EntryDate, l.Amount })
            .ToListAsync();

        // Payments linked to specific due lines (via LinkedEntryId)
        var dueIds = dues.Select(d => d.EntryId).ToList();
        var linkedPayments = await _db.LedgerEntries.IgnoreQueryFilters()
            .Where(l => l.AccountId == accountId
                        && l.EntryType == LedgerEntryType.PAYMENT_RECEIVED
                        && l.LinkedEntryId != null
                        && dueIds.Contains(l.LinkedEntryId.Value))
            .Select(l => new { l.LinkedEntryId, l.Amount })
            .ToListAsync();

        var paidByDueId = linkedPayments
            .GroupBy(p => p.LinkedEntryId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        return dues.Select(d =>
        {
            var paid = paidByDueId.GetValueOrDefault(d.EntryId, 0m);
            var balance = d.Amount - paid;
            string status = paid >= d.Amount ? "PAID" : "OVERDUE";
            return new DueLineDto(d.EntryId, d.EntryDate, d.Amount, paid, balance < 0 ? 0 : balance, status);
        }).ToList();
    }
}
