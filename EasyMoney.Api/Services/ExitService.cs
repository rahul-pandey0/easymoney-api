using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EasyMoney.Api.Services;

public interface IExitService
{
    Task<ExitResultDto> ProcessExitAsync(long accountId, DateOnly exitDate, PaymentMethod refundMethod);
}

public class ExitService : IExitService
{
    private readonly EasyMoneyDbContext _db;
    private readonly IAccountingService _accounting;
    private readonly ITenantContext _ctx;
    private readonly ILogger<ExitService> _log;

    public ExitService(EasyMoneyDbContext db, IAccountingService accounting, ITenantContext ctx, ILogger<ExitService> log)
    {
        _db = db; _accounting = accounting; _ctx = ctx; _log = log;
    }

    public async Task<ExitResultDto> ProcessExitAsync(long accountId, DateOnly exitDate, PaymentMethod refundMethod)
    {
        var a = await _db.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.AccountId == accountId)
            ?? throw new DomainException($"Account {accountId} not found");
        if (a.Status is AccountStatus.EXITED) throw new DomainException("Account already EXITED");
        if (a.Status is AccountStatus.COMPLETED) throw new DomainException("Account already COMPLETED");

        // Prized members (won a cycle) can still exit — their corpus was already
        // reduced at bid resolution time (forfeiture). The exit penalty applies to
        // total paid-in and the remaining corpus is refunded normally.

        var scheme = await _db.SchemeConfigs.IgnoreQueryFilters().FirstAsync(s => s.TenantId == a.TenantId);

        // Compute total paid-in = sum of PAYMENT_RECEIVED from this account's subsidiary ledger
        decimal totalPaidIn = await _db.LedgerEntries.IgnoreQueryFilters()
            .Where(l => l.AccountId == accountId && l.EntryType == LedgerEntryType.PAYMENT_RECEIVED)
            .SumAsync(l => (decimal?)l.Amount) ?? 0m;

        // Member account current balance (corpus owed to member, after any dividends already credited)
        decimal corpus = await _accounting.GetMemberAccountBalanceAsync(accountId);

        decimal penalty = Math.Round(totalPaidIn * scheme.EarlyExitPenaltyPct / 100m, 2);
        decimal refund = Math.Max(0m, corpus - penalty);

        long? penaltyJournalId = null;
        long? refundJournalId = null;

        // Penalty journal: Dr MEMBER_ACCOUNT (reduce corpus), Cr Penalty Income
        if (penalty > 0)
        {
            penaltyJournalId = await _accounting.PostJournalAsync(
                a.TenantId, exitDate, JournalSourceType.EXIT,
                sourceId: a.AccountId, paymentMethod: PaymentMethod.SYSTEM,
                description: $"Early-exit penalty {scheme.EarlyExitPenaltyPct}% of paid-in for {a.AccountNumber}",
                lines: new[]
                {
                    new JournalLineInput(EntryTarget.MEMBER_ACCOUNT, null, a.AccountId, penalty, 0),
                    new JournalLineInput(EntryTarget.GL, SystemGl.PenaltyIncome, null, 0, penalty)
                },
                createdBy: _ctx.UserId, authorizedBy: _ctx.UserId);

            _db.LedgerEntries.Add(new LedgerEntry
            {
                TenantId = a.TenantId,
                AccountId = a.AccountId,
                CycleId = null,
                EntryType = LedgerEntryType.EXIT_PENALTY,
                Amount = penalty,
                EntryDate = exitDate,
                Description = $"Early exit penalty {scheme.EarlyExitPenaltyPct}%",
                CreatedBy = _ctx.UserId
            });
        }

        // Refund journal: Dr MEMBER_ACCOUNT (zero out corpus), Cr Cash/Bank
        if (refund > 0)
        {
            var cashCode = refundMethod switch
            {
                PaymentMethod.CASH => SystemGl.CashInHand,
                PaymentMethod.BANK_TRANSFER or PaymentMethod.CHEQUE or PaymentMethod.NEFT or PaymentMethod.RTGS => SystemGl.Bank,
                PaymentMethod.EFT or PaymentMethod.UPI => SystemGl.EftClearing,
                _ => SystemGl.Bank
            };
            refundJournalId = await _accounting.PostJournalAsync(
                a.TenantId, exitDate, JournalSourceType.EXIT,
                sourceId: a.AccountId, paymentMethod: refundMethod,
                description: $"Exit refund to {a.AccountNumber} via {refundMethod}",
                lines: new[]
                {
                    new JournalLineInput(EntryTarget.MEMBER_ACCOUNT, null, a.AccountId, refund, 0),
                    new JournalLineInput(EntryTarget.GL, cashCode, null, 0, refund)
                },
                createdBy: _ctx.UserId, authorizedBy: _ctx.UserId);

            _db.LedgerEntries.Add(new LedgerEntry
            {
                TenantId = a.TenantId,
                AccountId = a.AccountId,
                CycleId = null,
                EntryType = LedgerEntryType.EXIT_REFUND,
                Amount = refund,
                EntryDate = exitDate,
                Description = $"Exit refund via {refundMethod}",
                CreatedBy = _ctx.UserId
            });
        }

        a.Status = AccountStatus.EXITED;
        await _db.SaveChangesAsync();

        _log.LogInformation("Exit {Aid}: paidIn={PaidIn}, penalty={Pen}, refund={Ref}",
            accountId, totalPaidIn, penalty, refund);
        return new ExitResultDto(a.AccountId, exitDate, totalPaidIn, penalty, refund,
            penaltyJournalId, refundJournalId);
    }
}
