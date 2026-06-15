using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EasyMoney.Api.Services;

public interface ILoanService
{
    /// <summary>Records the prize disbursement during cycle resolution.</summary>
    Task<Loan> DisburseAsync(long accountId, long cycleId, decimal principal);

    Task<Loan?> GetByAccountAsync(long accountId);
}

public class LoanService : ILoanService
{
    private readonly EasyMoneyDbContext _db;
    private readonly IAccountingService _accounting;
    private readonly ITenantContext _ctx;
    private readonly ILogger<LoanService> _log;

    public LoanService(EasyMoneyDbContext db, IAccountingService accounting, ITenantContext ctx, ILogger<LoanService> log)
    {
        _db = db; _accounting = accounting; _ctx = ctx; _log = log;
    }

    public async Task<Loan> DisburseAsync(long accountId, long cycleId, decimal principal)
    {
        if (principal <= 0) throw new DomainException("Principal must be > 0");
        var a = await _db.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.AccountId == accountId)
            ?? throw new DomainException($"Account {accountId} not found");
        if (await _db.Loans.IgnoreQueryFilters().AnyAsync(l => l.AccountId == accountId))
            throw new DomainException("Account already has a loan");

        var cycle = await _db.BiddingCycles.IgnoreQueryFilters().FirstAsync(c => c.CycleId == cycleId);

        var loan = new Loan
        {
            TenantId = a.TenantId,
            AccountId = a.AccountId,
            CycleId = cycleId,
            PrincipalAmount = principal,
            DisbursedAt = DateTime.UtcNow,
            AuthorizedBy = _ctx.UserId,
            AuthorizedAt = DateTime.UtcNow
        };
        _db.Loans.Add(loan);

        // Subsidiary ledger entry
        _db.LedgerEntries.Add(new LedgerEntry
        {
            TenantId = a.TenantId,
            AccountId = a.AccountId,
            CycleId = cycleId,
            EntryType = LedgerEntryType.LOAN_DISBURSEMENT,
            Amount = principal,
            EntryDate = cycle.CycleMonth,
            Description = $"Loan disbursed (cycle {cycle.CycleMonth:yyyy-MM})",
            CreatedBy = _ctx.UserId
        });
        await _db.SaveChangesAsync();

        // GL journal: Dr Loans Receivable (asset booked), Cr Bank (cash paid out).
        await _accounting.PostJournalAsync(
            a.TenantId, cycle.CycleMonth, JournalSourceType.LOAN_DISBURSEMENT,
            sourceId: loan.LoanId, paymentMethod: PaymentMethod.BANK_TRANSFER,
            description: $"Loan disbursement to {a.AccountNumber} (cycle {cycle.CycleMonth:yyyy-MM})",
            lines: new[]
            {
                new JournalLineInput(EntryTarget.GL, SystemGl.LoansReceivable, null, principal, 0),
                new JournalLineInput(EntryTarget.GL, SystemGl.Bank, null, 0, principal)
            },
            createdBy: _ctx.UserId, authorizedBy: _ctx.UserId);

        _log.LogInformation("Disbursed loan {Lid} ₹{P} to account {Aid}", loan.LoanId, principal, accountId);
        return loan;
    }

    public Task<Loan?> GetByAccountAsync(long accountId) =>
        _db.Loans.IgnoreQueryFilters().FirstOrDefaultAsync(l => l.AccountId == accountId);
}
