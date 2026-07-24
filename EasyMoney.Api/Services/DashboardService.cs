using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EasyMoney.Api.Services;

public class DashboardService
{
    private readonly EasyMoneyDbContext _db;

    public DashboardService(EasyMoneyDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardDto> GetDashboardAsync()
    {
        var dto = new DashboardDto();

        dto.TotalMembers =
            await _db.Members.CountAsync();

        dto.CustomersWithoutAccounts =
           await _db.Members.CountAsync(m =>
        !_db.Accounts.Any(a => a.MemberId == m.MemberId));

        dto.TotalAccounts =
            await _db.Accounts.CountAsync();

        dto.ActiveAccounts =
            await _db.Accounts.CountAsync(x =>
                x.Status == AccountStatus.ACTIVE);

        dto.TotalLoans =
            await _db.Loans.CountAsync();

        dto.ActiveLoans =
            await _db.Loans.CountAsync(x =>
                x.Status == LoanStatus.ACTIVE);

        dto.TotalTenants =
            await _db.Tenants.CountAsync();

        dto.PendingKyc =
            await _db.Members.CountAsync(x =>
                x.KycStatus == KycStatus.PENDING);

        dto.PendingApprovals =
            await _db.ApprovalRequests.CountAsync(x =>
                x.Status == ApprovalStatus.PENDING);

        dto.TotalCollections =
            await _db.LedgerEntries
                .SumAsync(x => (decimal?)x.Amount) ?? 0;

        dto.TotalLoanAmount =
            await _db.Loans
                .SumAsync(x => (decimal?)x.PrincipalAmount) ?? 0;

        return dto;
    }
}