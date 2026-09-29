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
        var members = _ctx.BypassTenantFilter ? _db.Members.IgnoreQueryFilters() : _db.Members;
        var accounts = _ctx.BypassTenantFilter ? _db.Accounts.IgnoreQueryFilters() : _db.Accounts;
        var loans = _ctx.BypassTenantFilter ? _db.Loans.IgnoreQueryFilters() : _db.Loans;
        var payment = _ctx.BypassTenantFilter ? _db.PaymentDetail.IgnoreQueryFilters() : _db.PaymentDetail;
        var branch = _ctx.BypassTenantFilter ? _db.Branches.IgnoreQueryFilters() : _db.Branches;
        var tenant = _ctx.BypassTenantFilter ? _db.Tenants.IgnoreQueryFilters() : _db.Tenants;


        var dto = new DashboardDto();

        dto.TotalMembers = await members.CountAsync();
        dto.TotalAccounts = await accounts.CountAsync();
        dto.ActiveAccounts = await accounts.CountAsync(
            a => a.Status == AccountStatus.ACTIVE ||
                 a.Status == AccountStatus.PRIZED
        ); dto.TotalLoans = await loans.CountAsync();
        dto.ActiveLoans = await loans.CountAsync(l => l.Status == "ACTIVE");
        dto.TotalTenants = await _db.Tenants.CountAsync();
        dto.PendingKyc = await members.CountAsync(m => m.KycStatus == KycStatus.PENDING);
        dto.ApprovedKyc = await members.CountAsync(m => m.KycStatus == KycStatus.APPROVED);

        dto.CustomersWithoutAccounts = await members
            .CountAsync(m => !accounts.Any(a => a.MemberId == m.MemberId));

        dto.TotalLoanAmount = await loans
            .SumAsync(x => (decimal?)x.PrincipalAmount) ?? 0;

        dto.TotalOutstandingBalance = await loans
            .SumAsync(l => (decimal?)l.OutstandingBalance) ?? 0;  // ✅ FIXED


        dto.LoanRecoveryPercentage = dto.TotalLoanAmount == 0
            ? 0
            : Math.Round((dto.TotalCollections / dto.TotalLoanAmount) * 100, 2);

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
                .CountAsync(a => a.Status == ApprovalStatus.PENDING && a.TenantId == tenantId);


            dto.TotalCollections = await _db.LedgerEntries
                .IgnoreQueryFilters()
                .Where(x => x.EntryType == LedgerEntryType.PAYMENT_RECEIVED &&  x.TenantId == tenantId)
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
               x.PaymentDate.ToDateTime(TimeOnly.MinValue) <= toDate.ToDateTime(TimeOnly.MinValue) && x.TenantId==_ctx.TenantId)
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