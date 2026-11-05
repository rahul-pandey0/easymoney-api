using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Dtos;
using Microsoft.EntityFrameworkCore;
using EasyMoney.Api.Domain;
using System.Security.Claims;

public class DashboardService
{
    private readonly EasyMoneyDbContext _db;
    private readonly ITenantContext _ctx;

    public DashboardService(EasyMoneyDbContext db, ITenantContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    //public async Task<DashboardDto> GetDashboardAsync()
    //{
    //    var members = _ctx.BypassTenantFilter
    //        ? _db.Members.IgnoreQueryFilters()
    //        : _db.Members;

    //    var accounts = _ctx.BypassTenantFilter
    //        ? _db.Accounts.IgnoreQueryFilters()
    //        : _db.Accounts;

    //    var loans = _ctx.BypassTenantFilter
    //        ? _db.Loans.IgnoreQueryFilters()
    //        : _db.Loans;

    //    var dto = new DashboardDto();

    //    dto.TotalMembers = await members.CountAsync();

    //    dto.CustomersWithoutAccounts =
    //        await members.CountAsync(m =>
    //            !accounts.Any(a => a.MemberId == m.MemberId));

    //    dto.TotalAccounts = await accounts.CountAsync();

    //    dto.ActiveAccounts =
    //        await accounts.CountAsync(a => a.Status == AccountStatus.ACTIVE);

    //    dto.TotalLoans = await loans.CountAsync();

    //    dto.ActiveLoans =
    //        await loans.CountAsync(l => l.Status =="ACTIVE");

    //    dto.TotalTenants = await _db.Tenants.CountAsync();

    //    dto.PendingKyc =
    //        await members.CountAsync(m => m.KycStatus == KycStatus.PENDING);

    //    bool isSifinAdmin = _ctx.Role == Roles.SifinAdmin || _ctx.Role == Roles.SifinOperator;

    //    if (isSifinAdmin)
    //    {
    //        // SIFIN admin → count across ALL tenants
    //        dto.PendingApprovals = await _db.ApprovalRequests
    //            .CountAsync(a => a.Status == ApprovalStatus.PENDING);
    //    }
    //    else
    //    {
    //        // Org admin → count only their tenant's
    //        var tenantId = _ctx.TenantId ?? 0;
    //        dto.PendingApprovals = await _db.ApprovalRequests
    //            .CountAsync(a => a.Status == ApprovalStatus.PENDING && a.TenantId == tenantId);
    //    }

    //    dto.TotalCollections = await _db.LedgerEntries
    //       .IgnoreQueryFilters()
    //       .Where(x => x.EntryType == LedgerEntryType.PAYMENT_RECEIVED)
    //       .SumAsync(x => (decimal?)x.Amount) ?? 0;

    //    dto.TotalLoanAmount =
    //        await loans.SumAsync(x => (decimal?)x.PrincipalAmount) ?? 0;
    //    dto.LoanRecoveryPercentage =
    //    dto.TotalLoanAmount == 0
    //    ? 0
    //    : Math.Round((dto.TotalCollections / dto.TotalLoanAmount) * 100, 2);

    //    dto.ApprovedKyc =
    //        await members.CountAsync(m => m.KycStatus == KycStatus.APPROVED);

    //    dto.TotalOutstandingBalance = await loans
    //        .SumAsync(l => (decimal?)l.OutstandingBalance) ?? 0;
    //    return dto;
    //}

    public async Task<DashboardDto> GetDashboardAsync()
    {
        var members = _ctx.BypassTenantFilter ? _db.Members.Where(a => a.TenantId ==_ctx.TenantId  && a.BranchId ==_ctx.BranchId) .IgnoreQueryFilters() : _db.Members;
        var accounts = _ctx.BypassTenantFilter ? _db.Accounts.Where(a => a.TenantId == _ctx.TenantId && a.BranchId == _ctx.BranchId) .IgnoreQueryFilters() : _db.Accounts;
        var loans = _ctx.BypassTenantFilter ? _db.Loans.Where(a => a.TenantId == _ctx.TenantId && a.BranchId == _ctx.BranchId).IgnoreQueryFilters() : _db.Loans;
        var payment = _ctx.BypassTenantFilter ? _db.PaymentDetail.Where(a => a.TenantId ==_ctx.TenantId  && a.BranchId ==_ctx.BranchId).IgnoreQueryFilters() : _db.PaymentDetail;
        var branch = _ctx.BypassTenantFilter ? _db.Branches.IgnoreQueryFilters() : _db.Branches;
        var tenant = _ctx.BypassTenantFilter ? _db.Tenants.IgnoreQueryFilters() : _db.Tenants;
        //var poolmoney = _ctx.BypassTenantFilter ? _db.LedgerEntries.Where(a => a.TenantId == _ctx.TenantId && a.BranchId == _ctx.BranchId).IgnoreQueryFilters() : _db.LedgerEntries;
        //var poolmoneycollections  = _ctx.BypassTenantFilter ? _db.PaymentDetail.Where(a => a.TenantId == _ctx.TenantId && a.BranchId == _ctx.BranchId && a.PaymentDate== _ctx.CurrentDate) .IgnoreQueryFilters() : _db.PaymentDetail;
        //var now = _ctx.CurrentDate; // whatever type: DateOnly, DateTime, etc.
        //int y = now.Year;
        //int m = now.Month;
        //var poolmoney = _ctx.BypassTenantFilter ? _db.LedgerEntries.IgnoreQueryFilters().Where(a => a.TenantId == _ctx.TenantId && a.BranchId == _ctx.BranchId && a.EntryDate.Year == y && a.EntryDate.Month == m) : _db.LedgerEntries;

        //var poolMoneyCollections = _ctx.BypassTenantFilter ? _db.PaymentDetail.IgnoreQueryFilters().Where(a => a.TenantId == _ctx.TenantId  && a.BranchId == _ctx.BranchId   && a.PaymentDate.Year == y  && a.PaymentDate.Month == m): _db.PaymentDetail;

        var now = _ctx.CurrentDate;
        int y = now.Year;
        int m = now.Month;
        var startOfMonth = new DateTime(y, m, 1);
        var startOfNextMonth = startOfMonth.AddMonths(1);

        // ============================================================
        // LEDGER ENTRIES  (join Account, exclude first-payment accounts)
        // ============================================================
        var ledgerQuery =
            from a in _db.LedgerEntries
            join acc in _db.Accounts on a.AccountId equals acc.AccountId
            where a.TenantId == _ctx.TenantId
               && a.BranchId == _ctx.BranchId
               && a.EntryDate >= DateOnly.FromDateTime(startOfMonth)
               && a.EntryDate < DateOnly.FromDateTime(startOfNextMonth)
               && acc.FirstPaymentFlag != "Y"          // "remaining" only
            select a;

        if (_ctx.BypassTenantFilter)
            ledgerQuery = ledgerQuery.IgnoreQueryFilters();

        // ============================================================
        // PAYMENT DETAILS  (join Account, exclude first-payment accounts)
        // ============================================================
        var paymentQuery =
            from p in _db.PaymentDetail
            join acc in _db.Accounts on p.AccountId equals acc.AccountId
            where p.TenantId == _ctx.TenantId
               && p.BranchId == _ctx.BranchId
               && p.PaymentDate >= DateOnly.FromDateTime(startOfMonth)
               && p.PaymentDate < DateOnly.FromDateTime(startOfNextMonth)
               && acc.FirstPaymentFlag != "Y"          // "remaining" only
            select p;

        if (_ctx.BypassTenantFilter)
            paymentQuery = paymentQuery.IgnoreQueryFilters();

        // ============================================================
        // SUM (sequential — DbContext is not thread-safe)
        // ============================================================
        decimal poolMoneyAmount = await ledgerQuery
            .SumAsync(a => (decimal?)a.Amount) ?? 0m;

        decimal poolMoneyCollectionAmount = await paymentQuery.SumAsync(a => (decimal?)a.Amount) ?? 0m;

        decimal combined = poolMoneyAmount + poolMoneyCollectionAmount;

        // Optional: total
        decimal combinedAmount = poolMoneyAmount + poolMoneyCollectionAmount;


        var dto = new DashboardDto();

        dto.TotalMembers = await members.CountAsync(a => a.TenantId == _ctx.TenantId && a.BranchId == _ctx.BranchId);
        dto.TotalAccounts = await accounts.CountAsync(a => a.TenantId == _ctx.TenantId && a.BranchId == _ctx.BranchId);
        dto.ActiveAccounts = await accounts.CountAsync(a => a.TenantId == _ctx.TenantId && a.BranchId == _ctx.BranchId && (a.Status == AccountStatus.ACTIVE || a.Status == AccountStatus.PRIZED)  );
        dto.TotalLoans = await loans.CountAsync(a => a.TenantId == _ctx.TenantId && a.BranchId == _ctx.BranchId);
        dto.ActiveLoans = await loans.CountAsync(l => l.Status == "ACTIVE");
        dto.TotalTenants = await _db.Tenants.CountAsync();
        dto.PendingKyc = await members.CountAsync(m => m.KycStatus == KycStatus.PENDING &&  m.TenantId == _ctx.TenantId && m.BranchId == _ctx.BranchId);
        dto.ApprovedKyc = await members.CountAsync(m => m.KycStatus == KycStatus.APPROVED && m.TenantId == _ctx.TenantId && m.BranchId == _ctx.BranchId);

        dto.CustomersWithoutAccounts = await members
            .CountAsync(m => !accounts.Any(a => a.MemberId == m.MemberId && m.TenantId == _ctx.TenantId && m.BranchId == _ctx.BranchId));

        dto.TotalLoanAmount = await loans.Where(m => m.TenantId == _ctx.TenantId && m.BranchId == _ctx.BranchId)
            .SumAsync(x => (decimal?)x.PrincipalAmount) ?? 0;

        dto.TotalOutstandingBalance = await loans.Where(m => m.TenantId == _ctx.TenantId && m.BranchId == _ctx.BranchId)
            .SumAsync(l => (decimal?)l.OutstandingBalance) ?? 0;  // ✅ FIXED


        dto.LoanRecoveryPercentage = dto.TotalLoanAmount == 0
            ? 0
            : Math.Round((dto.TotalCollections / dto.TotalLoanAmount) * 100, 2);
        dto.PoolMoney = poolMoneyAmount;
        dto.PoolMoneyCollections = poolMoneyCollectionAmount;

        // Pending Approvals — role-aware
        bool isSifinAdmin = _ctx.Role == Roles.SifinAdmin || _ctx.Role == Roles.SifinOperator;
        if (isSifinAdmin)
        {
            dto.PendingApprovals = await _db.ApprovalRequests
                .CountAsync(a => a.Status == ApprovalStatus.PENDING);


            dto.TotalCollections = await _db.LedgerEntries
                .IgnoreQueryFilters()
                .Where(x => x.EntryType == LedgerEntryType.PAYMENT_RECEIVED)
                .SumAsync(x => (decimal?)x.Amount) ?? 0;


            var tenantCounts = await tenant
                .Where(t => t.StartDate.HasValue)
                .GroupBy(t => new
                {
                    Year = t.StartDate.Value.Year,
                    Month = t.StartDate.Value.Month
                })
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.Month)
                .Select(g => new TenantCount
                {
                    Month = new DateTime(
                        g.Key.Year,
                        g.Key.Month,
                        1
                    ),
                    Count = g.Count()
                })
                .ToListAsync();

            dto.Count = tenantCounts;



                  dto.SuspendedTenantcount = await _db.Tenants
                .CountAsync(a => a.Status == TenantStatus.SUSPENDED);

        }


        else
        {
            var tenantId = _ctx.TenantId ?? 0;
            dto.PendingApprovals = await _db.ApprovalRequests
                .CountAsync(a => a.Status == ApprovalStatus.PENDING && a.TenantId == tenantId && a.BranchId == _ctx.BranchId);


            dto.TotalCollections = await _db.LedgerEntries
                .IgnoreQueryFilters()
                .Where(x => x.EntryType == LedgerEntryType.PAYMENT_RECEIVED &&  x.TenantId == tenantId && x.BranchId == _ctx.BranchId)
                .SumAsync(x => (decimal?)x.Amount) ?? 0;

            var branchs = await branch.FirstOrDefaultAsync(b => b.TenantId == _ctx.TenantId && b.BranchId == _ctx.BranchId);
            if (branch != null)
            {
                dto.PreviousDate = branchs.PreviousDate;
                dto.CurrentDate = branchs.CurrentDate;
                dto.NextDate = branchs.NextDate;
                dto.CutoffDate = branchs.CutoffDate;
                dto.BiddingDate = branchs.BiddingDate;
            }
        }
        var fromDate = _ctx.CurrentDate.AddDays(-6);
        var toDate = _ctx.CurrentDate;

        var dailyCollections = await payment
           .Where(x =>
               x.PaymentDate.ToDateTime(TimeOnly.MinValue) >= fromDate.ToDateTime(TimeOnly.MinValue) &&
               x.PaymentDate.ToDateTime(TimeOnly.MinValue) <= toDate.ToDateTime(TimeOnly.MinValue) && x.TenantId==_ctx.TenantId && x.BranchId == _ctx.BranchId)
           .GroupBy(x => x.PaymentDate)
           .Select(g => new DailyCollectionDto
           {
               Date = g.Key.ToDateTime(TimeOnly.MinValue), // Convert DateOnly to DateTime
               Amount = g.Sum(x => x.Amount)
           })
           .OrderBy(x => x.Date)
           .ToListAsync();

        dto.PaymentAmount = dailyCollections;



        return dto;
    }
}