using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace EasyMoney.Api.Jobs;

/// <summary>
/// Runs on the 1st of each month: opens a new bidding cycle for every active tenant.
/// Dues are no longer generated here — they are written upfront when an account is opened.
/// </summary>
public class MonthlyCycleJob
{
    private readonly EasyMoneyDbContext _db;
    private readonly IBiddingService _bidding;
    private readonly ILogger<MonthlyCycleJob> _log;

    public MonthlyCycleJob(EasyMoneyDbContext db, IBiddingService bidding, ILogger<MonthlyCycleJob> log)
    {
        _db = db; _bidding = bidding; _log = log;
    }

    public async Task ExecuteAsync()
    {
        var month = DateOnly.FromDateTime(DateTime.UtcNow);
        month = new DateOnly(month.Year, month.Month, 1);
        _log.LogInformation("MonthlyCycleJob starting for month {Month}", month);

        var tenants = await _db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Status == TenantStatus.ACTIVE)
            .Select(t => t.TenantId)
            .ToListAsync();

        foreach (var tenantId in tenants)
        {
            try
            {
                await _bidding.OpenCycleAsync(tenantId, month, biddingDate: null);
                _log.LogInformation("Opened cycle {Month} for tenant {TenantId}", month, tenantId);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "MonthlyCycleJob failed for tenant {TenantId}", tenantId);
            }
        }

        _log.LogInformation("MonthlyCycleJob completed for month {Month}", month);
    }
}
