using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EasyMoney.Api.Services;

public interface IBiddingService
{
    Task<BiddingCycle> OpenCycleAsync(long tenantId, DateOnly cycleMonth, DateOnly? biddingDate);
    Task<BiddingCycle?> GetCurrentCycleAsync(long tenantId);
    Task<BiddingCycle?> GetCycleAsync(long cycleId);
    Task<IReadOnlyList<Bid>> GetBidsAsync(long cycleId);
    Task<Bid> SubmitOrUpdateBidAsync(long accountId, decimal bidPct);
    Task<BiddingCycle> CloseBiddingAsync(long cycleId);
    Task<AwardPreviewDto> GetAwardPreviewAsync(long cycleId);
    Task<CycleResolutionResultDto> ResolveCycleAsync(long cycleId);
}

public class BiddingService : IBiddingService
{
    private readonly EasyMoneyDbContext _db;
    private readonly IAccountingService _accounting;
    private readonly ILoanService _loans;
    private readonly ITenantContext _ctx;
    private readonly ILogger<BiddingService> _log;

    public BiddingService(EasyMoneyDbContext db, IAccountingService accounting, ILoanService loans,
        ITenantContext ctx, ILogger<BiddingService> log)
    {
        _db = db; _accounting = accounting; _loans = loans; _ctx = ctx; _log = log;
    }

    public async Task<BiddingCycle> OpenCycleAsync(long tenantId, DateOnly cycleMonth, DateOnly? biddingDate)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        var monthStart = new DateOnly(cycleMonth.Year, cycleMonth.Month, 1);

        // Rule 1: cannot open a future month
        if (monthStart > currentMonthStart)
            throw new DomainException(
                $"Cannot open a future cycle. Requested: {monthStart:yyyy-MM}, current month: {currentMonthStart:yyyy-MM}");

        // Rule 2: the previous calendar month's cycle must be RESOLVED or NO_BID (or not exist yet for month 1)
        var prevMonthStart = monthStart.AddMonths(-1);
        var prevCycle = await _db.BiddingCycles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CycleMonth == prevMonthStart);
        if (prevCycle is not null && prevCycle.Status is not (CycleStatus.RESOLVED or CycleStatus.NO_BID))
            throw new DomainException(
                $"Cannot open {monthStart:yyyy-MM} — previous cycle ({prevMonthStart:yyyy-MM}) is still {prevCycle.Status}. Resolve it first.");

        // Rule 3: if cycle already exists for this month
        var existing = await _db.BiddingCycles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CycleMonth == monthStart);
        if (existing is not null)
        {
            if (existing.Status is CycleStatus.RESOLVED or CycleStatus.NO_BID)
                throw new DomainException(
                    $"Cycle for {monthStart:yyyy-MM} is already {existing.Status}. Each month can only have one cycle.");
            // OPEN or CLOSED — return as-is (idempotent)
            return existing;
        }

        var scheme = await _db.SchemeConfigs.IgnoreQueryFilters().FirstAsync(s => s.TenantId == tenantId);

        // Rule 4: biddingDate must be within the cycle month and not in the future beyond today
        var defaultBidDay = new DateOnly(monthStart.Year, monthStart.Month, scheme.BiddingDayOfMonth);
        var bd = biddingDate ?? defaultBidDay;

        // biddingDate must belong to the cycle's month
        if (bd.Year != monthStart.Year || bd.Month != monthStart.Month)
            throw new DomainException(
                $"biddingDate {bd:yyyy-MM-dd} must be within the cycle month {monthStart:yyyy-MM}");

        // biddingDate cannot be in the future (must be today or earlier — operator is recording a bid day they planned)
        // We allow it to be up to end of current month so they can plan ahead within the same month
        // but not beyond the current month itself (already covered by Rule 1 on cycleMonth)

        var openAt = DateTime.UtcNow;
        var closeAt = new DateTime(bd.Year, bd.Month, bd.Day, 23, 59, 59, DateTimeKind.Utc);

        var c = new BiddingCycle
        {
            TenantId = tenantId,
            CycleMonth = monthStart,
            WindowOpenAt = openAt,
            WindowCloseAt = closeAt,
            Status = CycleStatus.OPEN
        };
        _db.BiddingCycles.Add(c);
        await _db.SaveChangesAsync();
        _log.LogInformation("Opened cycle {Cid} for tenant {Tid} month {Month}, planned bid date {Bd}",
            c.CycleId, tenantId, monthStart, bd);
        return c;
    }

    public Task<BiddingCycle?> GetCurrentCycleAsync(long tenantId) =>
        _db.BiddingCycles.IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && (c.Status == CycleStatus.OPEN || c.Status == CycleStatus.CLOSED))
            .OrderByDescending(c => c.CycleMonth)
            .FirstOrDefaultAsync();

    public Task<BiddingCycle?> GetCycleAsync(long cycleId) =>
        _db.BiddingCycles.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.CycleId == cycleId);

    public async Task<IReadOnlyList<Bid>> GetBidsAsync(long cycleId) =>
        await _db.Bids.Where(b => b.CycleId == cycleId)
            .OrderByDescending(b => b.BidPct).ToListAsync();

    public async Task<Bid> SubmitOrUpdateBidAsync(long accountId, decimal bidPct)
    {
        var a = await _db.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.AccountId == accountId)
            ?? throw new DomainException($"Account {accountId} not found");
        if (a.Status != AccountStatus.ACTIVE) throw new DomainException($"Account is {a.Status}; cannot bid");
        if (a.IsPrized) throw new DomainException("Prized accounts cannot bid again");

        var scheme = await _db.SchemeConfigs.IgnoreQueryFilters().FirstAsync(s => s.TenantId == a.TenantId);
        if (a.InstallmentsPaid < scheme.MinInstallmentsForEligibility)
            throw new DomainException(
                $"Need at least {scheme.MinInstallmentsForEligibility} paid installments to bid (have {a.InstallmentsPaid})");
        if (bidPct < scheme.MinBidPct || bidPct > scheme.MaxBidPct)
            throw new DomainException($"Bid must be between {scheme.MinBidPct}% and {scheme.MaxBidPct}%");

        var cycle = await GetCurrentCycleAsync(a.TenantId)
            ?? throw new DomainException("No open cycle for this tenant");
        if (cycle.Status != CycleStatus.OPEN)
            throw new DomainException($"Bidding is not open — cycle is currently {cycle.Status}");

        var bid = await _db.Bids.FirstOrDefaultAsync(b => b.CycleId == cycle.CycleId && b.AccountId == a.AccountId);
        if (bid is null)
        {
            bid = new Bid
            {
                CycleId = cycle.CycleId,
                AccountId = a.AccountId,
                BidPct = bidPct,
                SubmittedAt = DateTime.UtcNow
            };
            _db.Bids.Add(bid);
        }
        else
        {
            bid.BidPct = bidPct;
            bid.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
        return bid;
    }

    public async Task<BiddingCycle> CloseBiddingAsync(long cycleId)
    {
        var cycle = await _db.BiddingCycles.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.CycleId == cycleId)
            ?? throw new DomainException($"Cycle {cycleId} not found");
        if (cycle.Status != CycleStatus.OPEN)
            throw new DomainException($"Cycle is already {cycle.Status}; cannot close bidding");
        cycle.Status = CycleStatus.CLOSED;
        cycle.WindowCloseAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _log.LogInformation("Bidding closed for cycle {Cid} by operator", cycleId);
        return cycle;
    }

    public async Task<AwardPreviewDto> GetAwardPreviewAsync(long cycleId)
    {
        var cycle = await _db.BiddingCycles.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.CycleId == cycleId)
            ?? throw new DomainException($"Cycle {cycleId} not found");
        if (cycle.Status == CycleStatus.OPEN)
            throw new DomainException("Bidding is still open — close bidding before previewing the award");

        var scheme = await _db.SchemeConfigs.IgnoreQueryFilters().FirstAsync(s => s.TenantId == cycle.TenantId);

        // Gross corpus = sum of monthly contributions of all participating accounts
        var participants = await _db.Accounts.IgnoreQueryFilters()
            .Where(a => a.TenantId == cycle.TenantId
                        && (a.Status == AccountStatus.ACTIVE || a.Status == AccountStatus.PRIZED)
                        && a.AccountOpenDate <= cycle.CycleMonth
                        && a.TenureEndDate > cycle.CycleMonth)
            .ToListAsync();
        var grossCorpus = participants.Sum(a => a.MonthlyContribution);
        var orgFeeAmount = Math.Round(grossCorpus * scheme.OrgFeePct / 100m, 2);
        var bidPool = grossCorpus - orgFeeAmount;

        // All bids for this cycle joined to account + member
        var bidsRaw = await (
            from b in _db.Bids.Where(b => b.CycleId == cycleId)
            join a in _db.Accounts.IgnoreQueryFilters() on b.AccountId equals a.AccountId
            join m in _db.Members.IgnoreQueryFilters() on a.MemberId equals m.MemberId
            select new { b, a, m }
        ).ToListAsync();

        // Rank: highest bidPct first, then highest contribution, then earliest open date, then lowest accountId
        var ranked = bidsRaw
            .OrderByDescending(x => x.b.BidPct)
            .ThenByDescending(x => x.a.MonthlyContribution)
            .ThenBy(x => x.a.AccountOpenDate)
            .ThenBy(x => x.a.AccountId)
            .Select((x, i) =>
            {
                var forfeiture = Math.Round(grossCorpus * x.b.BidPct / 100m, 2);
                var prize = bidPool - forfeiture;
                return new AwardPreviewBidDto(
                    Rank: i + 1,
                    BidId: x.b.BidId,
                    AccountId: x.a.AccountId,
                    AccountNumber: x.a.AccountNumber,
                    MemberId: x.m.MemberId,
                    MemberName: x.m.FullName,
                    MemberPhone: x.m.Phone,
                    BidPct: x.b.BidPct,
                    ForfeitureAmount: forfeiture,
                    PrizeIfWins: prize < 0 ? 0 : prize,
                    SubmittedAt: x.b.SubmittedAt,
                    UpdatedAt: x.b.UpdatedAt);
            })
            .ToList();

        return new AwardPreviewDto(
            cycle.CycleId, cycle.CycleMonth, cycle.Status.ToString(),
            grossCorpus, orgFeeAmount, bidPool,
            ranked.Count, ranked);
    }

    public async Task<CycleResolutionResultDto> ResolveCycleAsync(long cycleId)
    {
        var cycle = await _db.BiddingCycles.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.CycleId == cycleId)
            ?? throw new DomainException($"Cycle {cycleId} not found");
        if (cycle.Status is CycleStatus.RESOLVED or CycleStatus.NO_BID)
            throw new DomainException($"Cycle already {cycle.Status}");

        var scheme = await _db.SchemeConfigs.IgnoreQueryFilters().FirstAsync(s => s.TenantId == cycle.TenantId);

        // Step 1: close the cycle window
        cycle.Status = CycleStatus.CLOSED;
        await _db.SaveChangesAsync();

        // Step 2: gross corpus = sum(monthly_contribution) over participating accounts
        var participants = await _db.Accounts.IgnoreQueryFilters()
            .Where(a => a.TenantId == cycle.TenantId
                        && (a.Status == AccountStatus.ACTIVE || a.Status == AccountStatus.PRIZED)
                        && a.AccountOpenDate <= cycle.CycleMonth
                        && a.TenureEndDate > cycle.CycleMonth)
            .ToListAsync();
        var grossCorpus = participants.Sum(a => a.MonthlyContribution);
        var orgFeeAmount = Math.Round(grossCorpus * scheme.OrgFeePct / 100m, 2);
        var bidPool = grossCorpus - orgFeeAmount;

        if (grossCorpus <= 0)
        {
            cycle.GrossCorpus = 0m; cycle.OrgFeeAmount = 0m; cycle.BidPool = 0m;
            cycle.LoanDisbursed = 0m; cycle.DividendPool = 0m;
            cycle.Status = CycleStatus.NO_BID; cycle.ResolvedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return new CycleResolutionResultDto(cycleId, cycle.Status.ToString(),
                0m, 0m, 0m, null, null, 0m, 0m, 0, 0m);
        }

        // Step 3: SIFIN commission split (reclassifies a portion of upcoming org fee income).
        //         Posted before the winner-forfeiture journal so org fee revenue can split cleanly.
        decimal sifinShare = Math.Round(orgFeeAmount * scheme.SifinCommissionPct / 100m, 2);

        // Step 6: pick winner from eligible bids
        var eligibleBids = await _db.Bids
            .Where(b => b.CycleId == cycle.CycleId)
            .Join(_db.Accounts.IgnoreQueryFilters(),
                  b => b.AccountId, a => a.AccountId,
                  (b, a) => new { b, a })
            .Where(x => !x.a.IsPrized
                        && x.a.Status == AccountStatus.ACTIVE
                        && x.a.InstallmentsPaid >= scheme.MinInstallmentsForEligibility
                        && x.b.BidPct >= scheme.MinBidPct && x.b.BidPct <= scheme.MaxBidPct)
            .ToListAsync();

        long? winnerAccountId = null;
        decimal? winnerBidPct = null;
        decimal loanAmount = 0m;
        decimal dividendPool = 0m;

        int dividendCount = 0;
        if (eligibleBids.Count == 0)
        {
            // NO_BID path: dividend pool = NoBidDefaultDividendPct% of gross corpus,
            // funded by the org-fee + remaining corpus. Distribute over all eligible accounts.
            cycle.Status = CycleStatus.NO_BID;
            dividendPool = Math.Round(grossCorpus * scheme.NoBidDefaultDividendPct / 100m, 2);

            // Org fee journal (no winner forfeiture in NO_BID): the org fee comes pro rata from
            // each participating member's ACC.
            if (orgFeeAmount > 0)
                await PostFeeJournalAsync(cycle, participants, orgFeeAmount);
            if (sifinShare > 0)
                await PostSifinSplitAsync(cycle, sifinShare);

            dividendCount = await DistributeDividendForNoBidAsync(cycle, participants, dividendPool);
        }
        else
        {
            // Order: bid_pct DESC, monthly_contribution DESC (per user choice), open_date ASC, account_id ASC
            var winner = eligibleBids
                .OrderByDescending(x => x.b.BidPct)
                .ThenByDescending(x => x.a.MonthlyContribution)
                .ThenBy(x => x.a.AccountOpenDate)
                .ThenBy(x => x.a.AccountId)
                .First();

            winner.b.IsWinner = true;
            winner.a.IsPrized = true;
            winner.a.Status = AccountStatus.PRIZED;
            winnerAccountId = winner.a.AccountId;
            winnerBidPct = winner.b.BidPct;

            var winnerForfeiture = Math.Round(grossCorpus * winner.b.BidPct / 100m, 2);
            loanAmount = bidPool - winnerForfeiture;
            dividendPool = winnerForfeiture - orgFeeAmount;
            if (dividendPool < 0) dividendPool = 0;

            await _db.SaveChangesAsync();

            // Consolidated forfeiture-and-distribution journal:
            //   Dr Winner ACC      winnerForfeiture
            //   Cr 4000 Org Fee Income     orgFeeAmount
            //   Cr Non-winner ACC_i        share_i  (floored, sum <= dividendPool)
            //   Cr Winner ACC              residual (any cents that couldn't be allocated)
            dividendCount = await PostWinnerForfeitureAndDividendAsync(
                cycle, winner.a.AccountId, winnerForfeiture, orgFeeAmount, dividendPool);

            // SIFIN split as separate journal
            if (sifinShare > 0) await PostSifinSplitAsync(cycle, sifinShare);

            // Disburse loan via LoanService (Dr 1100, Cr Bank)
            if (loanAmount > 0)
                await _loans.DisburseAsync(winner.a.AccountId, cycle.CycleId, loanAmount);
        }

        cycle.GrossCorpus = grossCorpus;
        cycle.OrgFeeAmount = orgFeeAmount;
        cycle.BidPool = bidPool;
        cycle.WinnerAccountId = winnerAccountId;
        cycle.WinnerBidPct = winnerBidPct;
        cycle.LoanDisbursed = loanAmount;
        cycle.DividendPool = dividendPool;
        cycle.ResolvedAt = DateTime.UtcNow;
        if (cycle.Status != CycleStatus.NO_BID) cycle.Status = CycleStatus.RESOLVED;
        await _db.SaveChangesAsync();

        _log.LogInformation(
            "Resolved cycle {Cid}: gross={Gross}, orgFee={Fee}, sifin={Sifin}, winner={Win}, loan={Loan}, divPool={Div}, divCount={Cnt}",
            cycle.CycleId, grossCorpus, orgFeeAmount, sifinShare, winnerAccountId, loanAmount, dividendPool, dividendCount);

        return new CycleResolutionResultDto(
            cycle.CycleId, cycle.Status.ToString(),
            grossCorpus, orgFeeAmount, bidPool,
            winnerAccountId, winnerBidPct,
            loanAmount, dividendPool, dividendCount, sifinShare);
    }

    /// <summary>
    /// Org-fee-only journal (used in NO_BID path). Each participant's ACC is debited
    /// pro rata; 4000 Org Fee Income credited the total.
    /// </summary>
    private async Task PostFeeJournalAsync(BiddingCycle cycle, IReadOnlyList<Account> participants, decimal orgFeeAmount)
    {
        var grossCorpus = participants.Sum(p => p.MonthlyContribution);
        var lines = new List<JournalLineInput>();
        decimal allocated = 0m;
        for (int i = 0; i < participants.Count; i++)
        {
            var p = participants[i];
            decimal share = (i == participants.Count - 1)
                ? Math.Round(orgFeeAmount - allocated, 2)
                : Math.Round(orgFeeAmount * p.MonthlyContribution / grossCorpus, 2);
            allocated += share;
            if (share > 0)
                lines.Add(new JournalLineInput(EntryTarget.MEMBER_ACCOUNT, null, p.AccountId, share, 0));
        }
        lines.Add(new JournalLineInput(EntryTarget.GL, SystemGl.OrgFeeIncome, null, 0, orgFeeAmount));
        await _accounting.PostJournalAsync(
            cycle.TenantId, cycle.CycleMonth, JournalSourceType.BID_RESOLUTION,
            cycle.CycleId, PaymentMethod.SYSTEM,
            $"Cycle {cycle.CycleMonth:yyyy-MM} org fee (no-bid)", lines,
            _ctx.UserId, _ctx.UserId);
    }

    private async Task PostSifinSplitAsync(BiddingCycle cycle, decimal sifinShare)
    {
        await _accounting.PostJournalAsync(
            cycle.TenantId, cycle.CycleMonth, JournalSourceType.BID_RESOLUTION,
            cycle.CycleId, PaymentMethod.SYSTEM,
            $"Cycle {cycle.CycleMonth:yyyy-MM} SIFIN commission split",
            new[]
            {
                new JournalLineInput(EntryTarget.GL, SystemGl.OrgFeeIncome, null, sifinShare, 0),
                new JournalLineInput(EntryTarget.GL, SystemGl.SifinCommissionPayable, null, 0, sifinShare)
            },
            _ctx.UserId, _ctx.UserId);
    }

    /// <summary>
    /// Single balanced journal that does:
    ///   Dr Winner ACC      winnerForfeiture
    ///   Cr 4000 Org Fee Income     orgFeeAmount
    ///   Cr Non-winner ACC_i        share_i (floored to 2dp; remainder swept to winner's ACC as a credit-back)
    /// Records each share as a dividend + ledger_entry too.
    /// Returns the number of non-winner accounts that received a dividend.
    /// </summary>
    private async Task<int> PostWinnerForfeitureAndDividendAsync(
        BiddingCycle cycle, long winnerAccountId,
        decimal winnerForfeiture, decimal orgFeeAmount, decimal dividendPool)
    {
        var scheme = await _db.SchemeConfigs.IgnoreQueryFilters().FirstAsync(s => s.TenantId == cycle.TenantId);

        var eligible = await _db.Accounts.IgnoreQueryFilters()
            .Where(a => a.TenantId == cycle.TenantId
                        && a.AccountId != winnerAccountId
                        && (a.Status == AccountStatus.ACTIVE || a.Status == AccountStatus.PRIZED)
                        && a.InstallmentsPaid >= scheme.MinInstallmentsForEligibility
                        && a.AccountOpenDate <= cycle.CycleMonth
                        && a.TenureEndDate > cycle.CycleMonth)
            .ToListAsync();

        var lines = new List<JournalLineInput>
        {
            new(EntryTarget.MEMBER_ACCOUNT, null, winnerAccountId, winnerForfeiture, 0)
        };
        if (orgFeeAmount > 0)
            lines.Add(new JournalLineInput(EntryTarget.GL, SystemGl.OrgFeeIncome, null, 0, orgFeeAmount));

        int dividendCount = 0;
        decimal distributed = 0m;
        if (dividendPool > 0 && eligible.Count > 0)
        {
            var totalContribution = eligible.Sum(a => a.MonthlyContribution);
            if (totalContribution > 0)
            {
                foreach (var a in eligible)
                {
                    var raw = (a.MonthlyContribution / totalContribution) * dividendPool;
                    var share = Math.Floor(raw * 100m) / 100m;
                    if (share <= 0) continue;
                    lines.Add(new JournalLineInput(EntryTarget.MEMBER_ACCOUNT, null, a.AccountId, 0, share));
                    _db.Dividends.Add(new Dividend { CycleId = cycle.CycleId, AccountId = a.AccountId, Amount = share });
                    _db.LedgerEntries.Add(new LedgerEntry
                    {
                        TenantId = cycle.TenantId,
                        AccountId = a.AccountId,
                        CycleId = cycle.CycleId,
                        EntryType = LedgerEntryType.DIVIDEND_CREDIT,
                        Amount = share,
                        EntryDate = cycle.CycleMonth,
                        Description = $"Dividend for {cycle.CycleMonth:yyyy-MM}",
                        CreatedBy = _ctx.UserId
                    });
                    distributed += share;
                    dividendCount++;
                }
            }
        }

        // Residual = winnerForfeiture - orgFeeAmount - distributed. If positive (rounding cents),
        // credit back to the winner so the journal balances.
        decimal residual = Math.Round(winnerForfeiture - orgFeeAmount - distributed, 2);
        if (residual > 0)
            lines.Add(new JournalLineInput(EntryTarget.MEMBER_ACCOUNT, null, winnerAccountId, 0, residual));

        await _accounting.PostJournalAsync(
            cycle.TenantId, cycle.CycleMonth, JournalSourceType.BID_RESOLUTION,
            cycle.CycleId, PaymentMethod.SYSTEM,
            $"Cycle {cycle.CycleMonth:yyyy-MM} winner forfeiture + org fee + dividend",
            lines, _ctx.UserId, _ctx.UserId);

        await _db.SaveChangesAsync();
        return dividendCount;
    }

    /// <summary>
    /// NO_BID path: dividend pool is funded from each participant's ACC pro rata, distributed
    /// pro rata across the same participants. Net effect per participant: they pay a fee
    /// and receive a fraction back via dividend.
    /// </summary>
    private async Task<int> DistributeDividendForNoBidAsync(
        BiddingCycle cycle, IReadOnlyList<Account> participants, decimal dividendPool)
    {
        if (dividendPool <= 0 || participants.Count == 0) return 0;
        var totalContribution = participants.Sum(p => p.MonthlyContribution);
        if (totalContribution <= 0) return 0;

        var lines = new List<JournalLineInput>();
        decimal distributed = 0m;
        int count = 0;
        // Source side: Dr 5000 Dividend Expense (full pool)
        lines.Add(new JournalLineInput(EntryTarget.GL, SystemGl.DividendExpense, null, dividendPool, 0));
        foreach (var p in participants)
        {
            var raw = (p.MonthlyContribution / totalContribution) * dividendPool;
            var share = Math.Floor(raw * 100m) / 100m;
            if (share <= 0) continue;
            lines.Add(new JournalLineInput(EntryTarget.MEMBER_ACCOUNT, null, p.AccountId, 0, share));
            _db.Dividends.Add(new Dividend { CycleId = cycle.CycleId, AccountId = p.AccountId, Amount = share });
            _db.LedgerEntries.Add(new LedgerEntry
            {
                TenantId = cycle.TenantId,
                AccountId = p.AccountId,
                CycleId = cycle.CycleId,
                EntryType = LedgerEntryType.DIVIDEND_CREDIT,
                Amount = share,
                EntryDate = cycle.CycleMonth,
                Description = $"Dividend (no-bid) for {cycle.CycleMonth:yyyy-MM}",
                CreatedBy = _ctx.UserId
            });
            distributed += share;
            count++;
        }
        // If rounding leaves residual cents in 5000, credit them to the largest contributor
        var residual = Math.Round(dividendPool - distributed, 2);
        if (residual > 0 && participants.Count > 0)
        {
            var largest = participants.OrderByDescending(p => p.MonthlyContribution).First();
            lines.Add(new JournalLineInput(EntryTarget.MEMBER_ACCOUNT, null, largest.AccountId, 0, residual));
        }
        await _accounting.PostJournalAsync(
            cycle.TenantId, cycle.CycleMonth, JournalSourceType.DIVIDEND,
            cycle.CycleId, PaymentMethod.SYSTEM,
            $"Cycle {cycle.CycleMonth:yyyy-MM} no-bid dividend distribution",
            lines, _ctx.UserId, _ctx.UserId);
        await _db.SaveChangesAsync();
        return count;
    }

}
