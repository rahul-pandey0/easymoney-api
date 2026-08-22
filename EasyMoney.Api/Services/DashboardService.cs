using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Dtos;
using Microsoft.EntityFrameworkCore;
using EasyMoney.Api.Domain;

public class DashboardService
{
    private readonly EasyMoneyDbContext _db;
    private readonly ITenantContext _ctx;

    public DashboardService(EasyMoneyDbContext db, ITenantContext ctx)
    {
        _db = db;
        _ctx = ctx;
    }

    public async Task<DashboardDto> GetDashboardAsync()
    {
        var members = _ctx.BypassTenantFilter
            ? _db.Members.IgnoreQueryFilters()
            : _db.Members;

        var accounts = _ctx.BypassTenantFilter
            ? _db.Accounts.IgnoreQueryFilters()
            : _db.Accounts;

        var loans = _ctx.BypassTenantFilter
            ? _db.Loans.IgnoreQueryFilters()
            : _db.Loans;

        var dto = new DashboardDto();

        dto.TotalMembers = await members.CountAsync();

        dto.CustomersWithoutAccounts =
            await members.CountAsync(m =>
                !accounts.Any(a => a.MemberId == m.MemberId));

        dto.TotalAccounts = await accounts.CountAsync();

        dto.ActiveAccounts =
            await accounts.CountAsync(a => a.Status == AccountStatus.ACTIVE);

        dto.TotalLoans = await loans.CountAsync();

        dto.ActiveLoans =
            await loans.CountAsync(l => l.Status =="ACTIVE");

        dto.TotalTenants = await _db.Tenants.CountAsync();

        dto.PendingKyc =
            await members.CountAsync(m => m.KycStatus == KycStatus.PENDING);

        dto.PendingApprovals =
            await _db.ApprovalRequests.CountAsync(a => a.Status == ApprovalStatus.PENDING);

        dto.TotalCollections = await _db.LedgerEntries
           .IgnoreQueryFilters()
           .Where(x => x.EntryType == LedgerEntryType.PAYMENT_RECEIVED)
           .SumAsync(x => (decimal?)x.Amount) ?? 0;

        dto.TotalLoanAmount =
            await loans.SumAsync(x => (decimal?)x.PrincipalAmount) ?? 0;
        dto.LoanRecoveryPercentage =
        dto.TotalLoanAmount == 0
        ? 0
        : Math.Round((dto.TotalCollections / dto.TotalLoanAmount) * 100, 2);

        dto.ApprovedKyc =
            await members.CountAsync(m => m.KycStatus == KycStatus.APPROVED);

        return dto;
    }
}