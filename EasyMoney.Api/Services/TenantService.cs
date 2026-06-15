using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EasyMoney.Api.Services;

public interface ITenantService
{
    Task<Tenant> CreateTenantAsync(CreateTenantRequest req, long? createdBy, long? authorizedBy);
    Task SetTenantStatusAsync(long tenantId, TenantStatus status, long? updatedBy);
    Task UpdateSchemeConfigAsync(long tenantId, SchemeConfigUpdatePayload p, long? authorizedBy);
    Task<Tenant?> GetAsync(long tenantId);
    Task<IReadOnlyList<Tenant>> ListAsync();
    Task<SchemeConfig> GetSchemeConfigAsync(long tenantId);
}

public class TenantService : ITenantService
{
    private readonly EasyMoneyDbContext _db;
    private readonly IAccountingService _accounting;
    private readonly ILogger<TenantService> _log;

    public TenantService(EasyMoneyDbContext db, IAccountingService accounting, ILogger<TenantService> log)
    {
        _db = db; _accounting = accounting; _log = log;
    }

    public async Task<Tenant> CreateTenantAsync(CreateTenantRequest req, long? createdBy, long? authorizedBy)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) throw new DomainException("Tenant name required");
        if (await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Name == req.Name))
            throw new DomainException("Tenant name already exists");

        var t = new Tenant
        {
            Name = req.Name,
            RegistrationNumber = req.RegistrationNumber,
            Address = req.Address,
            Phone = req.Phone,
            OrgEmail = req.OrgEmail,
            ContactPersonName = req.ContactPersonName,
            ContactPersonPhone = req.ContactPersonPhone,
            Status = TenantStatus.ACTIVE,
            CreatedBy = createdBy,
            AuthorizedBy = authorizedBy,
            AuthorizedAt = authorizedBy.HasValue ? DateTime.UtcNow : null
        };
        _db.Tenants.Add(t);
        await _db.SaveChangesAsync();

        // 1:1 scheme_config row (defaults baked in via Domain entity property initializers)
        var sc = new SchemeConfig { TenantId = t.TenantId };
        _db.SchemeConfigs.Add(sc);
        await _db.SaveChangesAsync();

        // Seed the default Chart of Accounts (13 rows) per design §3.2
        await _accounting.SeedTenantChartAsync(t.TenantId, createdBy);

        _log.LogInformation("Created tenant {Tid} '{Name}' (reg={Reg}) with default scheme_config + CoA",
            t.TenantId, t.Name, t.RegistrationNumber);
        return t;
    }

    public async Task SetTenantStatusAsync(long tenantId, TenantStatus status, long? updatedBy)
    {
        var t = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.TenantId == tenantId)
            ?? throw new DomainException($"Tenant {tenantId} not found");
        t.Status = status;
        t.UpdatedBy = updatedBy;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task UpdateSchemeConfigAsync(long tenantId, SchemeConfigUpdatePayload p, long? authorizedBy)
    {
        var sc = await _db.SchemeConfigs.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.TenantId == tenantId)
            ?? throw new DomainException($"Tenant {tenantId} has no scheme_config");

        if (p.TenureMonths.HasValue) sc.TenureMonths = p.TenureMonths.Value;
        if (p.OrgFeePct.HasValue) sc.OrgFeePct = p.OrgFeePct.Value;
        if (p.SifinCommissionPct.HasValue) sc.SifinCommissionPct = p.SifinCommissionPct.Value;
        if (p.MinBidPct.HasValue) sc.MinBidPct = p.MinBidPct.Value;
        if (p.MaxBidPct.HasValue) sc.MaxBidPct = p.MaxBidPct.Value;
        if (p.EarlyExitPenaltyPct.HasValue) sc.EarlyExitPenaltyPct = p.EarlyExitPenaltyPct.Value;
        if (p.MinInstallmentsForEligibility.HasValue) sc.MinInstallmentsForEligibility = p.MinInstallmentsForEligibility.Value;
        if (p.BiddingWindowOpenDay.HasValue) sc.BiddingWindowOpenDay = p.BiddingWindowOpenDay.Value;
        if (p.BiddingDayOfMonth.HasValue) sc.BiddingDayOfMonth = p.BiddingDayOfMonth.Value;
        if (p.NoBidDefaultDividendPct.HasValue) sc.NoBidDefaultDividendPct = p.NoBidDefaultDividendPct.Value;
        if (!string.IsNullOrWhiteSpace(p.KycMode) && Enum.TryParse<KycMode>(p.KycMode, true, out var km))
            sc.KycMode = km;
        if (p.MakerCheckerEnabled.HasValue) sc.MakerCheckerEnabled = p.MakerCheckerEnabled.Value;

        // Sanity checks
        if (sc.MinBidPct >= sc.MaxBidPct)
            throw new DomainException("MinBidPct must be < MaxBidPct");
        if (sc.BiddingWindowOpenDay >= sc.BiddingDayOfMonth)
            throw new DomainException("BiddingWindowOpenDay must be < BiddingDayOfMonth");

        sc.UpdatedAt = DateTime.UtcNow;
        sc.AuthorizedBy = authorizedBy;
        sc.AuthorizedAt = authorizedBy.HasValue ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync();
    }

    public Task<Tenant?> GetAsync(long tenantId) =>
        _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.TenantId == tenantId);

    public async Task<IReadOnlyList<Tenant>> ListAsync() =>
        await _db.Tenants.IgnoreQueryFilters().OrderBy(t => t.TenantId).ToListAsync();

    public async Task<SchemeConfig> GetSchemeConfigAsync(long tenantId) =>
        await _db.SchemeConfigs.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.TenantId == tenantId)
            ?? throw new DomainException($"Tenant {tenantId} has no scheme_config");
}
