using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Ocsp;

namespace EasyMoney.Api.Services;

public interface IBiddingService
{
    //Task<BiddingCycle> OpenCycleAsync(long tenantId, DateOnly cycleMonth, DateOnly? biddingDate);
    Task<BiddingCycle> OpenCycleAsync(long tenantId, DateOnly cycleMonth, DateOnly? biddingDate = null);


    Task<BiddingCycle?> GetCurrentCycleAsync(long tenantId);
    Task<BiddingCycle?> GetCycleAsync(long cycleId);
    Task<Bid?> GetbidAsync(long accountid);
    Task<IReadOnlyList<Bid>> GetBidsAsync(long cycleId);
    Task<IReadOnlyList<Bid>> GetByData();

    //Task<Bid> SubmitOrUpdateBidAsync(long accountId, decimal bidPct);
    Task<Bid> SubmitOrUpdateBidAsync(long accountId, SubmitBidReq request);

    Task<BiddingCycle> CloseBiddingAsync(long cycleId);
    Task<AwardPreviewDto> GetAwardPreviewAsync(long cycleId);
    Task<CycleResolutionResultDto> ResolveCycleAsync(long cycleId);

    Task<Bid> ApproveBidAsync(long cycleId,long bidId);  
    Task<IReadOnlyList<Bid>> GetBidsAsync(long cycleId, string approvalStatus);
    Task<BiddingSummaryDto> GetCompleteBiddingSummaryAsync();
    Task<IReadOnlyList<Bonus>>GetBonusDetails();

    Task<IReadOnlyList<BonusDistribution>> GetBonusData();
     
    Task<Bonus> GetByBonusDetails(string refNo);  
     
    //Task SubmitOrUpdateBidAsync(long accountId, SubmitBidReq req);
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

    public async Task<BiddingCycle> OpenCycleAsync(long tenantId, DateOnly cycleMonth, DateOnly? biddingDate = null)
    {
        // Get branch from context
        var branch = await _db.Branches
            .Where(b => b.TenantId == tenantId && b.BranchId == _ctx.BranchId)
            .FirstOrDefaultAsync();

        if (branch == null)
            throw new DomainException("Branch not found");

        // Use branch's current date
        var today = branch.CurrentDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        var monthStart = new DateOnly(cycleMonth.Year, cycleMonth.Month, 1);

        // Rule 1: cannot open a future month
        if (monthStart > currentMonthStart)
            throw new DomainException(
                $"Cannot open a future cycle. Requested: {monthStart:yyyy-MM}, current month: {currentMonthStart:yyyy-MM}");

        // Rule 2: the previous calendar month's cycle must be RESOLVED or NO_BID
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
            return existing;
        }

        var scheme = await _db.SchemeConfigs.IgnoreQueryFilters().FirstAsync(s => s.TenantId == tenantId);

        // Calculate bidding date
        DateOnly bd;
        if (biddingDate.HasValue)
        {
            bd = biddingDate.Value;
            if (bd.Year != monthStart.Year || bd.Month != monthStart.Month)
                throw new DomainException(
                    $"biddingDate {bd:yyyy-MM-dd} must be within the cycle month {monthStart:yyyy-MM}");
        }
        else
        {
            var configuredBidDay = branch.BiddingDate?.Day ?? scheme.BiddingDayOfMonth;
            var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
            var bidDay = Math.Min(configuredBidDay, daysInMonth);
            bd = new DateOnly(monthStart.Year, monthStart.Month, bidDay);
        }

        var openAt = DateTime.UtcNow;
        var closeAt = new DateTime(bd.Year, bd.Month, bd.Day, 23, 59, 59, DateTimeKind.Utc);

        var c = new BiddingCycle
        {
            TenantId = tenantId,
            CycleMonth = (DateOnly)biddingDate,
            WindowOpenAt = openAt,
            WindowCloseAt = closeAt,
            Status = CycleStatus.OPEN,
            BranchId=(long)_ctx.BranchId  
        };
        _db.BiddingCycles.Add(c);
        await _db.SaveChangesAsync();

        _log.LogInformation("Opened cycle {Cid} for tenant {Tid} branch {BranchId} month {Month}, planned bid date {Bd}",
            c.CycleId, tenantId, branch.BranchId, monthStart, bd);

        return c;
    }

    //public async Task<BiddingCycle> OpenCycleAsync(long tenantId, DateOnly cycleMonth, DateOnly? biddingDate = null)
    //{
    //    var branch = await _db.Branches
    //        .Where(b => b.TenantId == _ctx.TenantId && b.BranchId == _ctx.BranchId)
    //        .FirstOrDefaultAsync();

    //    if (branch == null)
    //        throw new DomainException("Branch not found");

    //    // Use branch's current date instead of UTC
    //    var today = branch.CurrentDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
    //    var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
    //    var monthStart = new DateOnly(cycleMonth.Year, cycleMonth.Month, 1);

    //    // Rule 1: cannot open a future month
    //    if (monthStart > currentMonthStart)
    //        throw new DomainException(
    //            $"Cannot open a future cycle. Requested: {monthStart:yyyy-MM}, current month: {currentMonthStart:yyyy-MM}");

    //    // Rule 2: the previous calendar month's cycle must be RESOLVED or NO_BID (or not exist yet for month 1)
    //    var prevMonthStart = monthStart.AddMonths(-1);
    //    var prevCycle = await _db.BiddingCycles.IgnoreQueryFilters()
    //        .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CycleMonth == prevMonthStart);
    //    if (prevCycle is not null && prevCycle.Status is not (CycleStatus.RESOLVED or CycleStatus.NO_BID))
    //        throw new DomainException(
    //            $"Cannot open {monthStart:yyyy-MM} — previous cycle ({prevMonthStart:yyyy-MM}) is still {prevCycle.Status}. Resolve it first.");

    //    // Rule 3: if cycle already exists for this month
    //    var existing = await _db.BiddingCycles.IgnoreQueryFilters()
    //        .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CycleMonth == monthStart);
    //    if (existing is not null)
    //    {
    //        if (existing.Status is CycleStatus.RESOLVED or CycleStatus.NO_BID)
    //            throw new DomainException(
    //                $"Cycle for {monthStart:yyyy-MM} is already {existing.Status}. Each month can only have one cycle.");
    //        // OPEN or CLOSED — return as-is (idempotent)
    //        return existing;
    //    }

    //    var scheme = await _db.SchemeConfigs.IgnoreQueryFilters().FirstAsync(s => s.TenantId == tenantId);

    //    // **UPDATED: Automatic bidding date calculation based on branch's current date**
    //    DateOnly bd;

    //    if (biddingDate.HasValue)
    //    {
    //        // If bidding date is explicitly provided, use and validate it
    //        bd = biddingDate.Value;

    //        // biddingDate must belong to the cycle's month
    //        if (bd.Year != monthStart.Year || bd.Month != monthStart.Month)
    //            throw new DomainException(
    //                $"biddingDate {bd:yyyy-MM-dd} must be within the cycle month {monthStart:yyyy-MM}");
    //    }
    //    else
    //    {
    //        // Use branch's configured bidding date or scheme default
    //        var configuredBidDay = branch.BiddingDate?.Day ?? scheme.BiddingDayOfMonth;

    //        // Calculate bidding date for the cycle month
    //        var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
    //        var bidDay = Math.Min(configuredBidDay, daysInMonth);
    //        bd = new DateOnly(monthStart.Year, monthStart.Month, bidDay);
    //    }

    //    var openAt = DateTime.UtcNow;
    //    var closeAt = new DateTime(bd.Year, bd.Month, bd.Day, 23, 59, 59, DateTimeKind.Utc);

    //    var c = new BiddingCycle
    //    {
    //        TenantId = tenantId,
    //        CycleMonth = monthStart,
    //        WindowOpenAt = openAt,
    //        WindowCloseAt = closeAt,
    //        Status = CycleStatus.OPEN
    //    };
    //    _db.BiddingCycles.Add(c);
    //    await _db.SaveChangesAsync();

    //    _log.LogInformation("Opened cycle {Cid} for tenant {Tid} branch {BranchId} month {Month}, planned bid date {Bd}",
    //        c.CycleId, tenantId, branch.BranchId, monthStart, bd);

    //    return c;
    //}
    //public async Task<BiddingCycle> OpenCycleAsync(long tenantId, DateOnly cycleMonth, DateOnly? biddingDate)
    //{
    //    //var branch = await _db.Branches.Where(b => b.TenantId == _ctx.TenantId && b.BranchId == _ctx.BranchId)
    //    //          .FirstOrDefaultAsync();


    //    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    //    var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
    //    var monthStart = new DateOnly(cycleMonth.Year, cycleMonth.Month, 1);

    //    // Rule 1: cannot open a future month
    //    if (monthStart > currentMonthStart)
    //        throw new DomainException(
    //            $"Cannot open a future cycle. Requested: {monthStart:yyyy-MM}, current month: {currentMonthStart:yyyy-MM}");

    //    // Rule 2: the previous calendar month's cycle must be RESOLVED or NO_BID (or not exist yet for month 1)
    //    var prevMonthStart = monthStart.AddMonths(-1);
    //    var prevCycle = await _db.BiddingCycles.IgnoreQueryFilters()
    //        .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CycleMonth == prevMonthStart);
    //    if (prevCycle is not null && prevCycle.Status is not (CycleStatus.RESOLVED or CycleStatus.NO_BID))
    //        throw new DomainException(
    //            $"Cannot open {monthStart:yyyy-MM} — previous cycle ({prevMonthStart:yyyy-MM}) is still {prevCycle.Status}. Resolve it first.");

    //    // Rule 3: if cycle already exists for this month
    //    var existing = await _db.BiddingCycles.IgnoreQueryFilters()
    //        .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.CycleMonth == monthStart);
    //    if (existing is not null)
    //    {
    //        if (existing.Status is CycleStatus.RESOLVED or CycleStatus.NO_BID)
    //            throw new DomainException(
    //                $"Cycle for {monthStart:yyyy-MM} is already {existing.Status}. Each month can only have one cycle.");
    //        // OPEN or CLOSED — return as-is (idempotent)
    //        return existing;
    //    }

    //    var scheme = await _db.SchemeConfigs.IgnoreQueryFilters().FirstAsync(s => s.TenantId == tenantId);

    //    // Rule 4: biddingDate must be within the cycle month and not in the future beyond today
    //    var defaultBidDay = new DateOnly(monthStart.Year, monthStart.Month, scheme.BiddingDayOfMonth);
    //    var bd = biddingDate ?? defaultBidDay;

    //    // biddingDate must belong to the cycle's month
    //    if (bd.Year != monthStart.Year || bd.Month != monthStart.Month)
    //        throw new DomainException(
    //            $"biddingDate {bd:yyyy-MM-dd} must be within the cycle month {monthStart:yyyy-MM}");

    //    // biddingDate cannot be in the future (must be today or earlier — operator is recording a bid day they planned)
    //    // We allow it to be up to end of current month so they can plan ahead within the same month
    //    // but not beyond the current month itself (already covered by Rule 1 on cycleMonth)

    //    var openAt = DateTime.UtcNow;
    //    var closeAt = new DateTime(bd.Year, bd.Month, bd.Day, 23, 59, 59, DateTimeKind.Utc);

    //    var c = new BiddingCycle
    //    {
    //        TenantId = tenantId,
    //        CycleMonth = monthStart,
    //        WindowOpenAt = openAt,
    //        WindowCloseAt = closeAt,
    //        Status = CycleStatus.OPEN
    //    };
    //    _db.BiddingCycles.Add(c);
    //    await _db.SaveChangesAsync();
    //    _log.LogInformation("Opened cycle {Cid} for tenant {Tid} month {Month}, planned bid date {Bd}",
    //        c.CycleId, tenantId, monthStart, bd);
    //    return c;
    //}

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

    public Task<Bid?> GetbidAsync(long accountId) =>  
   _db.Bids.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.AccountId == accountId); 
    public async Task<IReadOnlyList<Bid>> GetByData() =>
    await _db.Bids.Where(b => b.TenantId == _ctx.TenantId && b.IsApproved==false).OrderByDescending(b => b.BidPct).ToListAsync();
    //public async Task<Bid> SubmitOrUpdateBidAsync(long accountId, decimal bidPct)
    //{
    //    var a = await _db.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.AccountId == accountId)
    //        ?? throw new DomainException($"Account {accountId} not found");
    //    if (a.Status != AccountStatus.ACTIVE) throw new DomainException($"Account is {a.Status}; cannot bid");
    //    if (a.IsPrized) throw new DomainException("Prized accounts cannot bid again");

    //    var scheme = await _db.SchemeConfigs.IgnoreQueryFilters().FirstAsync(s => s.TenantId == a.TenantId);
    //    if (a.InstallmentsPaid < scheme.MinInstallmentsForEligibility)
    //        throw new DomainException(
    //            $"Need at least {scheme.MinInstallmentsForEligibility} paid installments to bid (have {a.InstallmentsPaid})");
    //    if (bidPct < scheme.MinBidPct || bidPct > scheme.MaxBidPct)
    //        throw new DomainException($"Bid must be between {scheme.MinBidPct}% and {scheme.MaxBidPct}%");

    //    var cycle = await GetCurrentCycleAsync(a.TenantId)
    //        ?? throw new DomainException("No open cycle for this tenant");
    //    if (cycle.Status != CycleStatus.OPEN)
    //        throw new DomainException($"Bidding is not open — cycle is currently {cycle.Status}");

    //    var bid = await _db.Bids.FirstOrDefaultAsync(b => b.CycleId == cycle.CycleId && b.AccountId == a.AccountId);
    //    if (bid is null)
    //    {
    //        bid = new Bid
    //        {
    //            CycleId = cycle.CycleId,
    //            AccountId = a.AccountId,
    //            BidPct = bidPct,
    //            SubmittedAt = DateTime.UtcNow,
    //            IsApproved = false,
    //            TenantId = _ctx.TenantId,
    //            BranchId=  _ctx.BranchId
    //        };
    //        _db.Bids.Add(bid);
    //    }
    //    else
    //    {
    //        bid.BidPct = bidPct;
    //        bid.UpdatedAt = DateTime.UtcNow;
    //        bid.IsApproved = false;  // ADD THIS - reset approval on update
    //        bid.ApprovedAt = null;
    //        bid.ApprovedBy = null;
    //    }
    //    await _db.SaveChangesAsync();

    //    var approval = new ApprovalRequest
    //    {
    //        TenantId = _ctx.TenantId,
    //        ActionType = ApprovalActionType.BID_APPROVE,
    //        EntityType = "BIDDING",
    //        EntityId = _ctx.TenantId,
    //        Payload = System.Text.Json.JsonSerializer.Serialize(bid),
    //        Status = ApprovalStatus.PENDING,
    //        RequestedBy = _ctx.UserId.Value,
    //        RequestedAt = DateTime.UtcNow
    //    };
    //    _db.ApprovalRequests.Add(approval);

    //    return bid;
    //}
    public async Task<Bid> SubmitOrUpdateBidAsync(long accountId, SubmitBidReq req)
    { 
        var a = await _db.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.AccountId == accountId)
            ?? throw new DomainException($"Account {accountId} not found");
        if (a.Status != AccountStatus.ACTIVE) throw new DomainException($"Account is {a.Status}; cannot bid");
        if (a.IsPrized) throw new DomainException("Prized accounts cannot bid again");

        var scheme = await _db.SchemeConfigs.IgnoreQueryFilters().FirstAsync(s => s.TenantId == a.TenantId);
        if (a.InstallmentsPaid < scheme.MinInstallmentsForEligibility)
            throw new DomainException(
                $"Need at least {scheme.MinInstallmentsForEligibility} paid installments to bid (have {a.InstallmentsPaid})");
        if (req.BidPct < scheme.MinBidPct || req.BidPct > scheme.MaxBidPct)
            throw new DomainException($"Bid must be between {scheme.MinBidPct}% and {scheme.MaxBidPct}%");

        var cycle = await GetCurrentCycleAsync(a.TenantId)
            ?? throw new DomainException("No open cycle for this tenant");
        if (cycle.Status != CycleStatus.OPEN)
            throw new DomainException($"Bidding is not open — cycle is currently {cycle.Status}"); 

        var data = await _db.SchemeConfigs.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.TenantId == a.TenantId);

        var bid = await _db.Bids.FirstOrDefaultAsync(b => b.CycleId == cycle.CycleId && b.AccountId == a.AccountId);
        if (bid is null)
        {
            bid = new Bid
            {
                CycleId = cycle.CycleId,
                AccountId = a.AccountId,
                BidPct = req.BidPct,
                SubmittedAt = DateTime.UtcNow,
                IsApproved = false,
                TenantId = _ctx.TenantId,
                BranchId = _ctx.BranchId,
                OrgFeePct = data.OrgFeePct,
                SifinCommissionPct = data.SifinCommissionPct,
                FixedRate = req.FixedRate,
                //TotalBid=req.TotalBid,
                TragetAmount = req.TargetAmount,
                AllotmentAmount = req.AllotmentAmount,
                TotalBid = req.BidPct + data.TenantCommission,
                TenantCommissionPct = data.TenantCommission,

            };
            _db.Bids.Add(bid);
        }
        else
        {
            bid.BidPct = req.BidPct;
            bid.UpdatedAt = DateTime.UtcNow;
            bid.IsApproved = false;
            bid.ApprovedAt = null;
            bid.ApprovedBy = null;
            bid.OrgFeePct = data.OrgFeePct;
            bid.SifinCommissionPct = data.SifinCommissionPct;
        }

        // Save bid first to get BidId
        await _db.SaveChangesAsync();

        // Now create approval request with the actual BidId
        var approval = new ApprovalRequest
        {
            TenantId = _ctx.TenantId,
            ActionType = ApprovalActionType.BID_APPROVE,  
            EntityType = "BID",
            EntityId = bid.BidId, 
            Payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                bid.BidId,
                bid.CycleId,
                bid.AccountId,
                bid.BidPct,
                bid.IsApproved,
                TenantId = _ctx.TenantId,
                RequestedBy = _ctx.UserId
            }),
            Status = ApprovalStatus.PENDING,
            RequestedBy = _ctx.UserId.Value,
            RequestedAt = DateTime.UtcNow
        };

        _db.ApprovalRequests.Add(approval);
        await _db.SaveChangesAsync();

        return bid;
    }
    public async Task<BiddingCycle> CloseBiddingAsync(long cycleId) 
    {
        var totalBids = await _db.Bids.IgnoreQueryFilters().CountAsync(c => c.CycleId == cycleId);

        var approvedBids = await _db.Bids.IgnoreQueryFilters().CountAsync(c => c.CycleId == cycleId && c.IsApproved);

        if (totalBids == 0)
        {
            throw new DomainException($"No bids found for Cycle {cycleId}");
        }

        if (totalBids != approvedBids)
        {
            throw new DomainException($"Please approve all values. {approvedBids}/{totalBids} bids are approved");
        }


         var bids = await _db.Bids.IgnoreQueryFilters() .Where(b => b.CycleId == cycleId) .OrderBy(b => b.BidId).ToListAsync();
        var referenceDateTime = DateTime.UtcNow;
        foreach (var bid in bids)
        {
            bid.BidReferenceNo =
                $"{bid.TenantId}{bid.BranchId}{referenceDateTime:yyyyMMddHHmmss}-{bid.CycleId}";
        }


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

        // Step 3: pick winner from eligible bids (ONLY APPROVED BIDS)
        var eligibleBids = await _db.Bids
            .Where(b => b.CycleId == cycle.CycleId && b.IsApproved) // Only approved bids
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
            // NO_BID path
            cycle.Status = CycleStatus.NO_BID;
            dividendPool = Math.Round(grossCorpus * scheme.NoBidDefaultDividendPct / 100m, 2);

            if (orgFeeAmount > 0)
                await PostFeeJournalAsync(cycle, participants, orgFeeAmount);

            var sifinShare = Math.Round(orgFeeAmount * scheme.SifinCommissionPct / 100m, 2);
            if (sifinShare > 0)
                await PostSifinSplitAsync(cycle, sifinShare);

            dividendCount = await DistributeDividendForNoBidAsync(cycle, participants, dividendPool);
            

        }
        else
        {
            // Select winner
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

            // Post journals
            //dividendCount = await PostWinnerForfeitureAndDividendAsync(
            //    cycle, winner.a.AccountId, winnerForfeiture, orgFeeAmount, dividendPool);

            //var sifinShare = Math.Round(orgFeeAmount * scheme.SifinCommissionPct / 100m, 2);
            //if (sifinShare > 0) await PostSifinSplitAsync(cycle, sifinShare);

            // CREATE LOAN IN PENDING STATUS (NOT DISBURSED YET)
            //if (loanAmount > 0)
            //{
            //    var loan = new Loan
            //    {
            //        TenantId = cycle.TenantId,
            //        AccountId = winner.a.AccountId,
            //        CycleId = cycle.CycleId,
            //        PrincipalAmount = loanAmount,
            //        //DisbursedAt = null, // Not disbursed yet
            //        //Status = LoanStatus.PENDING, // Pending approval
            //        AuthStatus = false,
            //        CreatedAt = DateTime.UtcNow,
            //        CreatedBy = _ctx.UserId,
            //        BranchId = _ctx.BranchId,
            //        LoanRemark = $"Prize loan for cycle {cycle.CycleMonth:yyyy-MM}",
            //        OutstandingBalance = loanAmount
            //    };

            //    _db.Loans.Add(loan);
            //    await _db.SaveChangesAsync();

            //    // Create approval request for loan
            //    var approval = new ApprovalRequest
            //    {
            //        TenantId = _ctx.TenantId,
            //        ActionType = ApprovalActionType.CREATE_LOAN,
            //        EntityType = "Loan",
            //        EntityId = loan.LoanId,
            //        Payload = System.Text.Json.JsonSerializer.Serialize(new
            //        {
            //            loan.LoanId,
            //            loan.CycleId,
            //            loan.AccountId,
            //            loan.PrincipalAmount,
            //            cycle.CycleMonth,
            //            winnerBidPct,
            //            winnerForfeiture,
            //            orgFeeAmount,
            //            //sifinShare,
            //            dividendPool
            //        }),
            //        Status = ApprovalStatus.PENDING,
            //        RequestedBy = _ctx.UserId.Value,
            //        RequestedAt = DateTime.UtcNow
            //    };

            //    _db.ApprovalRequests.Add(approval);
            //    await _db.SaveChangesAsync();

            //    _log.LogInformation("Loan {LoanId} created for winner account {AccountId}, awaiting approval",
            //        loan.LoanId, winner.a.AccountId);
            //}
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

        return new CycleResolutionResultDto(
            cycle.CycleId, cycle.Status.ToString(),
            grossCorpus, orgFeeAmount, bidPool,
            winnerAccountId, winnerBidPct,
            loanAmount, dividendPool, dividendCount,
            Math.Round(orgFeeAmount * scheme.SifinCommissionPct / 100m, 2));
    }

    //public async Task<CycleResolutionResultDto> ResolveCycleAsync(long cycleId)
    //{
    //    var cycle = await _db.BiddingCycles.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.CycleId == cycleId)
    //        ?? throw new DomainException($"Cycle {cycleId} not found");
    //    if (cycle.Status is CycleStatus.RESOLVED or CycleStatus.NO_BID)
    //        throw new DomainException($"Cycle already {cycle.Status}");

    //    var scheme = await _db.SchemeConfigs.IgnoreQueryFilters().FirstAsync(s => s.TenantId == cycle.TenantId);

    //    // Step 1: close the cycle window
    //    cycle.Status = CycleStatus.CLOSED;
    //    await _db.SaveChangesAsync();

    //    // Step 2: gross corpus = sum(monthly_contribution) over participating accounts
    //    var participants = await _db.Accounts.IgnoreQueryFilters()
    //        .Where(a => a.TenantId == cycle.TenantId
    //                    && (a.Status == AccountStatus.ACTIVE || a.Status == AccountStatus.PRIZED)
    //                    && a.AccountOpenDate <= cycle.CycleMonth
    //                    && a.TenureEndDate > cycle.CycleMonth)
    //        .ToListAsync();
    //    var grossCorpus = participants.Sum(a => a.MonthlyContribution);
    //    var orgFeeAmount = Math.Round(grossCorpus * scheme.OrgFeePct / 100m, 2);
    //    var bidPool = grossCorpus - orgFeeAmount;

    //    if (grossCorpus <= 0)
    //    {
    //        cycle.GrossCorpus = 0m; cycle.OrgFeeAmount = 0m; cycle.BidPool = 0m;
    //        cycle.LoanDisbursed = 0m; cycle.DividendPool = 0m;
    //        cycle.Status = CycleStatus.NO_BID; cycle.ResolvedAt = DateTime.UtcNow;
    //        await _db.SaveChangesAsync();
    //        return new CycleResolutionResultDto(cycleId, cycle.Status.ToString(),
    //            0m, 0m, 0m, null, null, 0m, 0m, 0, 0m);
    //    }

    //    // Step 3: SIFIN commission split (reclassifies a portion of upcoming org fee income).
    //    //         Posted before the winner-forfeiture journal so org fee revenue can split cleanly.
    //    decimal sifinShare = Math.Round(orgFeeAmount * scheme.SifinCommissionPct / 100m, 2);

    //    // Step 6: pick winner from eligible bids
    //    var eligibleBids = await _db.Bids
    //        .Where(b => b.CycleId == cycle.CycleId)
    //        .Join(_db.Accounts.IgnoreQueryFilters(),
    //              b => b.AccountId, a => a.AccountId,
    //              (b, a) => new { b, a })
    //        .Where(x => !x.a.IsPrized
    //                    && x.a.Status == AccountStatus.ACTIVE
    //                    && x.a.InstallmentsPaid >= scheme.MinInstallmentsForEligibility
    //                    && x.b.BidPct >= scheme.MinBidPct && x.b.BidPct <= scheme.MaxBidPct)
    //        .ToListAsync();

    //    long? winnerAccountId = null;
    //    decimal? winnerBidPct = null;
    //    decimal loanAmount = 0m;
    //    decimal dividendPool = 0m;

    //    int dividendCount = 0;
    //    if (eligibleBids.Count == 0)
    //    {
    //        // NO_BID path: dividend pool = NoBidDefaultDividendPct% of gross corpus,
    //        // funded by the org-fee + remaining corpus. Distribute over all eligible accounts.
    //        cycle.Status = CycleStatus.NO_BID;
    //        dividendPool = Math.Round(grossCorpus * scheme.NoBidDefaultDividendPct / 100m, 2);

    //        // Org fee journal (no winner forfeiture in NO_BID): the org fee comes pro rata from
    //        // each participating member's ACC.
    //        if (orgFeeAmount > 0)
    //            await PostFeeJournalAsync(cycle, participants, orgFeeAmount);
    //        if (sifinShare > 0)
    //            await PostSifinSplitAsync(cycle, sifinShare);

    //        dividendCount = await DistributeDividendForNoBidAsync(cycle, participants, dividendPool);
    //    }
    //    else
    //    {
    //        // Order: bid_pct DESC, monthly_contribution DESC (per user choice), open_date ASC, account_id ASC
    //        var winner = eligibleBids
    //            .OrderByDescending(x => x.b.BidPct)
    //            .ThenByDescending(x => x.a.MonthlyContribution)
    //            .ThenBy(x => x.a.AccountOpenDate)
    //            .ThenBy(x => x.a.AccountId)
    //            .First();

    //        winner.b.IsWinner = true;
    //        winner.a.IsPrized = true;
    //        winner.a.Status = AccountStatus.PRIZED;
    //        winnerAccountId = winner.a.AccountId;
    //        winnerBidPct = winner.b.BidPct;

    //        var winnerForfeiture = Math.Round(grossCorpus * winner.b.BidPct / 100m, 2);
    //        loanAmount = bidPool - winnerForfeiture;
    //        dividendPool = winnerForfeiture - orgFeeAmount;
    //        if (dividendPool < 0) dividendPool = 0;

    //        await _db.SaveChangesAsync();

    //        // Consolidated forfeiture-and-distribution journal:
    //        //   Dr Winner ACC      winnerForfeiture
    //        //   Cr 4000 Org Fee Income     orgFeeAmount
    //        //   Cr Non-winner ACC_i        share_i  (floored, sum <= dividendPool)
    //        //   Cr Winner ACC              residual (any cents that couldn't be allocated)
    //        dividendCount = await PostWinnerForfeitureAndDividendAsync(
    //            cycle, winner.a.AccountId, winnerForfeiture, orgFeeAmount, dividendPool);

    //        // SIFIN split as separate journal
    //        if (sifinShare > 0) await PostSifinSplitAsync(cycle, sifinShare);

    //        // Disburse loan via LoanService (Dr 1100, Cr Bank)
    //        if (loanAmount > 0)
    //            await _loans.DisburseAsync(winner.a.AccountId, cycle.CycleId, loanAmount);
    //    }

    //    cycle.GrossCorpus = grossCorpus;
    //    cycle.OrgFeeAmount = orgFeeAmount;
    //    cycle.BidPool = bidPool;
    //    cycle.WinnerAccountId = winnerAccountId;
    //    cycle.WinnerBidPct = winnerBidPct;
    //    cycle.LoanDisbursed = loanAmount;
    //    cycle.DividendPool = dividendPool;
    //    cycle.ResolvedAt = DateTime.UtcNow;
    //    if (cycle.Status != CycleStatus.NO_BID) cycle.Status = CycleStatus.RESOLVED;
    //    await _db.SaveChangesAsync();

    //    _log.LogInformation(
    //        "Resolved cycle {Cid}: gross={Gross}, orgFee={Fee}, sifin={Sifin}, winner={Win}, loan={Loan}, divPool={Div}, divCount={Cnt}",
    //        cycle.CycleId, grossCorpus, orgFeeAmount, sifinShare, winnerAccountId, loanAmount, dividendPool, dividendCount);

    //    return new CycleResolutionResultDto(
    //        cycle.CycleId, cycle.Status.ToString(),
    //        grossCorpus, orgFeeAmount, bidPool,
    //        winnerAccountId, winnerBidPct,
    //        loanAmount, dividendPool, dividendCount, sifinShare);
    //}

    /// <summary>
    /// Org-fee-only journal (used in NO_BID path). Each participant's ACC is debited
    /// pro rata; 4000 Org Fee Income credited the total.
    /// </summary>
    private async Task PostFeeJournalAsync(BiddingCycle cycle, IReadOnlyList<Account> participants, decimal orgFeeAmount)
    {

        var data = await _db.SchemeConfigs.IgnoreQueryFilters().FirstAsync(s => s.TenantId == _ctx.TenantId);
        var orgfee = await _db.GeneralLedgerMaster.IgnoreQueryFilters().FirstAsync(s => s.GlId == data.OrgFeeGlId);
        var sifinfee = await _db.GeneralLedgerMaster.IgnoreQueryFilters().FirstAsync(s => s.GlId == data.SifinCommissionGlId);


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
        lines.Add(new JournalLineInput(EntryTarget.GL, orgfee.Code, null, 0, orgFeeAmount));
        await _accounting.PostJournalAsync(
            cycle.TenantId, cycle.CycleMonth, JournalSourceType.BID_RESOLUTION,
            cycle.CycleId, PaymentMethod.SYSTEM,
            $"Cycle {cycle.CycleMonth:yyyy-MM} org fee (no-bid)", lines,
            _ctx.UserId, _ctx.UserId);
    }

    private async Task PostSifinSplitAsync(BiddingCycle cycle, decimal sifinShare)
    {
        var data = await _db.SchemeConfigs.IgnoreQueryFilters().FirstAsync(s => s.TenantId == _ctx.TenantId);
        var orgfee = await _db.GeneralLedgerMaster.IgnoreQueryFilters().FirstAsync(s => s.GlId == data.OrgFeeGlId);
        var sifinfee = await _db.GeneralLedgerMaster.IgnoreQueryFilters().FirstAsync(s => s.GlId == data.SifinCommissionGlId); 

        await _accounting.PostJournalAsync(
            cycle.TenantId, cycle.CycleMonth, JournalSourceType.BID_RESOLUTION,
            cycle.CycleId, PaymentMethod.SYSTEM,
            $"Cycle {cycle.CycleMonth:yyyy-MM} SIFIN commission split",
            new[]
            {
                //new JournalLineInput(EntryTarget.GL, SystemGl.OrgFeeIncome, null, sifinShare, 0),
                new JournalLineInput(EntryTarget.GL,orgfee.Code, null, sifinShare, 0),
                new JournalLineInput(EntryTarget.GL, sifinfee.Code.ToString(), null, 0, sifinShare)
                //new JournalLineInput(EntryTarget.GL, SystemGl.SifinCommissionPayable, null, 0, sifinShare)
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

    public async Task<Bid> ApproveBidAsync(long cycleId, long bidId)
    {
        var cycle = await _db.BiddingCycles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.CycleId == cycleId);

        if (cycle is null)
            throw new DomainException($"Cycle {cycleId} not found.");

        // Get the specific bid that needs approval
        var bid = await _db.Bids.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.CycleId == cycleId && x.BidId == bidId && !x.IsApproved);

        if (bid is null)
            throw new DomainException($"Bid {bidId} is either already approved or not found.");

        // Approve this specific bid
        bid.IsApproved = true;
        bid.ApprovedAt = DateTime.UtcNow;
        bid.ApprovedBy = _ctx.UserId;
        bid.BranchId = _ctx.BranchId;

        await _db.SaveChangesAsync();

        return bid;
    }
    public async Task<IReadOnlyList<Bid>> GetBidsAsync( long cycleId, string? approvalStatus = null)
    {
        var query = _db.Bids .Where(b => b.CycleId == cycleId);

        if (string.Equals(approvalStatus, "pending",StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(b => !b.IsApproved);
        }
        else if (string.Equals( approvalStatus,"approved", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(b => b.IsApproved);
        }

        return await query
            .OrderByDescending(b => b.UpdatedAt ?? b.SubmittedAt)
            .ThenByDescending(b => b.BidPct)
            .ToListAsync();
    }
    public async Task<BiddingSummaryDto> GetCompleteBiddingSummaryAsync()
    {
        var tenantId = _ctx.TenantId ?? 0;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Get the current cycle (OPEN, CLOSED, or RESOLVED)
        var cycle = await _db.BiddingCycles
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId
                       && (c.Status == CycleStatus.OPEN
                           || c.Status == CycleStatus.CLOSED
                           || c.Status == CycleStatus.RESOLVED
                           || c.Status == CycleStatus.NO_BID))
            .OrderByDescending(c => c.CycleMonth)
            .FirstOrDefaultAsync();

        if (cycle == null)
        {
            return new BiddingSummaryDto
            {
                HasActiveBidding = false,
                Message = "No bidding cycle found",
                NextStep = "Open a new cycle to start bidding"
            };
        }

        // Get all bids for this cycle
        var allBids = await _db.Bids
            .Where(b => b.CycleId == cycle.CycleId)
            .ToListAsync();

        var approvedBids = allBids.Where(b => b.IsApproved).ToList();
        var pendingBids = allBids.Where(b => !b.IsApproved).ToList();

        // Get account details
        var accountIds = allBids.Select(b => b.AccountId).Distinct().ToList();
        var accounts = await _db.Accounts
            .IgnoreQueryFilters()
            .Where(a => accountIds.Contains(a.AccountId))
            .ToDictionaryAsync(a => a.AccountId);

        // Get member details
        var memberIds = accounts.Values.Select(a => a.MemberId).Distinct().ToList();
        var members = await _db.Members
            .IgnoreQueryFilters()
            .Where(m => memberIds.Contains(m.MemberId))
            .ToDictionaryAsync(m => m.MemberId);

        // Get scheme config for calculations
        var scheme = await _db.SchemeConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId);

        // Calculate corpus
        var participants = await _db.Accounts
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId
                        && (a.Status == AccountStatus.ACTIVE || a.Status == AccountStatus.PRIZED)
                        && a.AccountOpenDate <= cycle.CycleMonth
                        && a.TenureEndDate > cycle.CycleMonth)
            .ToListAsync();

        var grossCorpus = participants.Sum(a => a.MonthlyContribution);
        var orgFeeAmount = Math.Round(grossCorpus * (scheme?.OrgFeePct ?? 0) / 100m, 2);
        var bidPool = grossCorpus - orgFeeAmount;

        // Build bidder list with ranks
        var bidders = new List<BidderSummaryDto>();
        int rank = 1;

        // Sort by bid percentage (highest first)
        foreach (var bid in allBids.OrderByDescending(b => b.BidPct))
        {
            var account = accounts.GetValueOrDefault(bid.AccountId);
            var member = account != null ? members.GetValueOrDefault(account.MemberId) : null;

            if (account != null && member != null)
            {
                var forfeiture = Math.Round(grossCorpus * bid.BidPct / 100m, 2);
                var prizeIfWins = bidPool - forfeiture;

                string status;
                string badge;

                if (bid.IsWinner)
                {
                    status = "Winner";
                    badge = "winner";
                }
                else if (bid.IsApproved)
                {
                    status = "Approved";
                    badge = "approved";
                }
                else
                {
                    status = "Pending";
                    badge = "pending";
                }

                bidders.Add(new BidderSummaryDto
                {
                    //Rank = rank++,
                    BidId = bid.BidId,
                    AccountId = bid.AccountId,
                    AccountNumber = account.AccountNumber,
                    MemberId = member.MemberId,
                    MemberName = member.FullName,
                    MemberPhone = member.Phone,
                    BidPct = bid.BidPct,
                    SubmittedAt = bid.SubmittedAt,
                    IsApproved = bid.IsApproved,
                    IsWinner = bid.IsWinner,
                    ApprovedAt = bid.ApprovedAt,
                    ApprovedBy = bid.ApprovedBy,
                    MonthlyContribution = account.MonthlyContribution,
                    InstallmentsPaid = account.InstallmentsPaid,
                    //TotalInstallments = account.TotalInstallments,
                    ForfeitureAmount = forfeiture,
                    PrizeIfWins = prizeIfWins < 0 ? 0 : prizeIfWins,
                    Status = status,
                    StatusBadge = badge,
                    TotalBid = bid.TotalBid,
                    FixedRate =bid.FixedRate,
                    TargetAmount =bid.TragetAmount,
                    AllotmentAmount =bid.AllotmentAmount,
                });
            }
        }

        // Get winner
        WinnerSummaryDto winner = null;
        if (cycle.WinnerAccountId.HasValue)
        {
            var winnerBid = allBids.FirstOrDefault(b => b.AccountId == cycle.WinnerAccountId.Value && b.IsWinner);
            var winnerAccount = winnerBid != null ? accounts.GetValueOrDefault(winnerBid.AccountId) : null;
            var winnerMember = winnerAccount != null ? members.GetValueOrDefault(winnerAccount.MemberId) : null;

            if (winnerBid != null && winnerAccount != null && winnerMember != null)
            {
                var forfeiture = Math.Round(grossCorpus * winnerBid.BidPct / 100m, 2);
                var prize = bidPool - forfeiture;

                winner = new WinnerSummaryDto
                {
                    BidId = winnerBid.BidId,
                    AccountId = winnerAccount.AccountId,
                    AccountNumber = winnerAccount.AccountNumber,
                    MemberId = winnerMember.MemberId,
                    MemberName = winnerMember.FullName,
                    MemberPhone = winnerMember.Phone,
                    Email = winnerMember.Email,
                    BidPct = winnerBid.BidPct,
                    ForfeitureAmount = forfeiture,
                    PrizeAmount = prize < 0 ? 0 : prize,
                    NetReceivable = (prize < 0 ? 0 : prize) - (scheme?.SifinCommissionPct ?? 0),
                    SubmittedAt = winnerBid.SubmittedAt
                };
            }
        }

        // Determine status and next steps
        string statusText = cycle.Status.ToString();
        string nextStep = "";
        bool canProceed = false;

        switch (cycle.Status)
        {
            case CycleStatus.OPEN:
                statusText = "Bidding Open";
                nextStep = "Bidding is currently open. Close bidding to proceed.";
                canProceed = allBids.Any();
                break;
            case CycleStatus.CLOSED:
                statusText = "Bidding Closed";
                nextStep = "Bidding is closed. Select winner and resolve cycle.";
                canProceed = approvedBids.Any();
                break;
            case CycleStatus.RESOLVED:
                statusText = "Resolved";
                nextStep = "Cycle resolved. Winner selected and loan created.";
                canProceed = true;
                break;
            case CycleStatus.NO_BID:
                statusText = "No Bids";
                nextStep = "No eligible bids. Cycle completed without winner.";
                canProceed = true;
                break;
        }

        return new BiddingSummaryDto
        {
            HasActiveBidding = cycle.Status == CycleStatus.OPEN,
            CycleId = cycle.CycleId,
            CycleMonth = cycle.CycleMonth,
            CycleStatus = statusText,

            WindowOpenAt = cycle.WindowOpenAt,
            WindowCloseAt = cycle.WindowCloseAt,
            ClosedAt = cycle.WindowCloseAt,
            ResolvedAt = cycle.ResolvedAt,

            TotalBids = allBids.Count,
            ApprovedBids = approvedBids.Count,
            PendingBids = pendingBids.Count,
            RejectedBids = 0,
            UniqueBidders = accountIds.Count,

            GrossCorpus = grossCorpus,
            OrgFeeAmount = orgFeeAmount,
            BidPool = bidPool,
            LoanDisbursed = cycle.LoanDisbursed ?? 0,
            DividendPool = cycle.DividendPool ?? 0,

            Winner = winner,
            WinnerAccountId = cycle.WinnerAccountId,
            WinnerBidPct = cycle.WinnerBidPct,

            Bidders = bidders,

            Message = GetStatusMessage(cycle, allBids.Count, accountIds.Count),
            NextStep = nextStep,
            CanProceed = canProceed
        };
    }

    private string GetStatusMessage(BiddingCycle cycle, int totalBids, int uniqueBidders)
    {
        return cycle.Status switch
        {
            CycleStatus.OPEN => $"🔄 Bidding is OPEN - {totalBids} bids from {uniqueBidders} bidders",
            CycleStatus.CLOSED => $"📌 Bidding CLOSED - {totalBids} bids received",
            CycleStatus.RESOLVED => $"✅ Cycle RESOLVED - Winner selected!",
            CycleStatus.NO_BID => $"❌ No eligible bids - Cycle completed",
            _ => $"Status: {cycle.Status}"
        };
    }

    public async Task<IReadOnlyList<Bonus>> GetBonusDetails()
    {
        var branch = await _db.Branches
            .Where(b => b.TenantId == _ctx.TenantId && b.BranchId == _ctx.BranchId)
            .FirstOrDefaultAsync();

        if (branch == null)
        {
            return new List<Bonus>().AsReadOnly();
        }

        var query = _db.Bonus .Where(b => b.DistributedStatus == "N" && b.TenantId == branch.TenantId && b.BranchId == branch.BranchId);

        return await query.ToListAsync();
    }


    public async Task<Bonus> GetByBonusDetails(string refNo)
    {
        var branch = await _db.Branches
            .Where(b => b.TenantId == _ctx.TenantId && b.BranchId == _ctx.BranchId)
            .FirstOrDefaultAsync();

        // Get single record by reference number
        var bonus = await _db.Bonus
            .Where(b => b.BidReferenceNo == refNo)
            .FirstOrDefaultAsync();

        return bonus; 
    } 
    public async Task<IReadOnlyList<BonusDistribution>> GetBonusData() 
    {
        var branch = await _db.Branches
            .Where(b => b.TenantId == _ctx.TenantId && b.BranchId == _ctx.BranchId)
            .FirstOrDefaultAsync();

        var currentDate = branch.CurrentDate;

        var startOfMonth = currentDate.HasValue
           ? new DateTime(currentDate.Value.Year, currentDate.Value.Month, 1)
           : throw new InvalidOperationException("currentDate must have a value.");
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

        var query = _db.BonusDistribution
               .Where(b => b.TenantId == branch.TenantId
                   && b.BranchId == branch.BranchId
                   && b.DistributionDate >= startOfMonth
                   && b.DistributionDate <= endOfMonth
                   && b.Status == "DISTRIBUTED")
               .OrderByDescending(b => b.DistributionDate)
               .ThenByDescending(b => b.BonusAmount);

        return await query.ToListAsync();
    }



}
