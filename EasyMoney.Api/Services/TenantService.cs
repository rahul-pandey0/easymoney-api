using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EasyMoney.Api.Services;

public interface ITenantService
{
    Task<Tenant> CreateTenantAsync(CreateTenantRequest req, long? createdBy, long? authorizedBy, bool isSuperAdmin);
    Task SetTenantStatusAsync(long tenantId, TenantStatus status, long? updatedBy);
    Task UpdateSchemeConfigAsync(long tenantId, SchemeConfigUpdatePayload p, long? authorizedBy);
    Task<Tenant?> GetAsync(long tenantId);
    Task<IReadOnlyList<Tenant>> ListAsync();
    Task<SchemeConfig> GetSchemeConfigAsync(long tenantId);
    Task<SchemeMaster> GetSchemeConfigAsync();

    Task CreateSchemeConfigAsync(long tenantId, SchemeConfigUpdatePayload req, long? authorizedBy);
    Task CreateSchemeConfigAsync(SchemeConfigUpdatePayload req, long? authorizedBy);

    Task<IReadOnlyList<SchemeSummaryDto>> GetAllSchemeSummariesAsync();

    Task<IReadOnlyList<ProductSummaryDto>> GetAllProductSummariesAsync();

    Task<ProductSummaryDto> GetProductSummaryByIdAsync(int schemeId);

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

    public async Task<Tenant> CreateTenantAsync(CreateTenantRequest req, long? createdBy, long? authorizedBy, bool isSuperAdmin)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) throw new DomainException("Tenant name required");
        if (await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Name == req.Name))
            throw new DomainException("Tenant name already exists");

        //var t = new Tenant
        //{
        //    Name = req.Name,
        //    RegistrationNumber = req.RegistrationNumber,
        //    Address = req.Address,
        //    Phone = req.Phone,
        //    OrgEmail = req.OrgEmail,
        //    ContactPersonName = req.ContactPersonName,
        //    ContactPersonPhone = req.ContactPersonPhone,
        //    StartDate = req.StartDate,
        //    EffectiveDate = req.EffectiveDate,
        //    //Status = TenantStatus.ACTIVE,
        //    Status = isSuperAdmin? TenantStatus.ACTIVE: TenantStatus.PENDING,
        //    CreatedBy = createdBy,
        //    AuthorizedBy = authorizedBy,
        //    AuthorizedAt = authorizedBy.HasValue ? DateTime.UtcNow : null,

        //};

        var t = new Tenant
        {
            Name = req.Name,
            RegistrationNumber = req.RegistrationNumber,
            Address = req.Address,
            Phone = req.Phone,
            OrgEmail = req.OrgEmail,
            ContactPersonName = req.ContactPersonName,
            ContactPersonPhone = req.ContactPersonPhone,

            StartDate = req.StartDate,
            EffectiveDate = req.EffectiveDate,

            Status = isSuperAdmin
        ? TenantStatus.ACTIVE
        : TenantStatus.PENDING,

            CreatedBy = createdBy,

            AuthorizedBy = isSuperAdmin
        ? createdBy
        : null,

            AuthorizedAt = isSuperAdmin
        ? DateTime.UtcNow
        : null
        };
        _db.Tenants.Add(t);
        await _db.SaveChangesAsync();

        if (!isSuperAdmin)
        {
            if (!createdBy.HasValue)
                throw new DomainException("Created user required for maker-checker request");

            var approval = new ApprovalRequest
            {
                TenantId = t.TenantId,
                ActionType = ApprovalActionType.TENANT_CREATE,
                EntityType = "TENANT",
                EntityId = t.TenantId,
                Payload = System.Text.Json.JsonSerializer.Serialize(req),
                Status = ApprovalStatus.PENDING,
                RequestedBy = createdBy.Value,
                RequestedAt = DateTime.UtcNow
            };

            _db.ApprovalRequests.Add(approval);
            await _db.SaveChangesAsync();
        }

        // 1:1 scheme_config row (defaults baked in via Domain entity property initializers)
        //var sc = new SchemeConfig { TenantId = t.TenantId };
        //_db.SchemeConfigs.Add(sc);
        //await _db.SaveChangesAsync();

        // Seed the default Chart of Accounts (13 rows) per design §3.2
        await _accounting.SeedTenantChartAsync(t.TenantId, createdBy);

        _log.LogInformation("Created tenant {Tid} '{Name}' (reg={Reg}) with default scheme_config + CoA",
            t.TenantId, t.Name, t.RegistrationNumber);
        return t;
    }

    public async Task CreateSchemeConfigAsync(long tenantId, SchemeConfigUpdatePayload p, long? authorizedBy)
    {
        // Check whether the tenant exists
        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId);

        if (tenant == null)
            throw new DomainException($"Tenant '{tenantId}' does not exist.");

        // Check whether a scheme configuration already exists
        bool schemeExists = await _db.SchemeConfigs
            .IgnoreQueryFilters()
            .AnyAsync(s => s.TenantId == tenantId);

        if (schemeExists)
            throw new DomainException($"Scheme configuration already exists for tenant '{tenantId}'.");

        var schemeConfig = new SchemeConfig
        {
            TenantId = tenantId,

            TenureMonths = p.TenureMonths ?? 20,
            OrgFeePct = p.OrgFeePct ?? 5.00m,
            SifinCommissionPct = p.SifinCommissionPct ?? 1.00m,
            MinBidPct = p.MinBidPct ?? 15.00m,
            MaxBidPct = p.MaxBidPct ?? 50.00m,
            EarlyExitPenaltyPct = p.EarlyExitPenaltyPct ?? 15.00m,
            MinInstallmentsForEligibility = p.MinInstallmentsForEligibility ?? 2,
            BiddingWindowOpenDay = p.BiddingWindowOpenDay ?? 1,
            BiddingDayOfMonth = p.BiddingDayOfMonth ?? 15,
            NoBidDefaultDividendPct = p.NoBidDefaultDividendPct ?? 0.00m,

            KycMode = Enum.TryParse<KycMode>(p.KycMode, true, out var mode)
                ? mode
                : KycMode.MINIMAL_FIRST,

            MakerCheckerEnabled = p.MakerCheckerEnabled ?? true,

            // Bank Details
            BankName = p.BankName,
            CustAddress1 = p.CustAddress1,
            CustAddress2 = p.CustAddress2,
            CustAddress3 = p.CustAddress3,
            Email = p.Email,
            PhNum = p.PhNum,
            RdStatus = p.RdStatus,

            // Bonus
            GrossBonus = p.GrossBonus ?? 0m,
            TenantCommission = p.TenantCommission ?? 0m,
            NetBonus = p.NetBonus ?? 0m,

            // GL / Reserve
            Reserve1 = p.Reserve1,
            Reserve2 = p.Reserve2,
            PoolMoney = p.PoolMoney,
            TenantPin = p.TenantPin,
            LoanAssetGL = p.LoanAssetGL,
            SifinPayable = p.SifinPayable,

            // Time Change
            TimeChPass = p.TimeChPass ?? 0m,

            // Penalty
            PenaltyAcc = p.PenaltyAcc,
            NMPenaltyAcc = p.NMPenaltyAcc,

            // Interest
            MinimumRate = p.MinimumRate ?? 0m,
            MaximumRate = p.MaximumRate ?? 0m,
            MinimumPeriod = p.MinimumPeriod ?? 0,
            MaximumPeriod = p.MaximumPeriod ?? 0,

            // Tax
            TdsAc = p.TdsAc,
            ServicesTax = p.ServicesTax,

            // Audit
            UpdatedBy = authorizedBy,
            UpdatedAt = DateTime.UtcNow,
            AuthorizedBy = authorizedBy,
            AuthorizedAt = DateTime.UtcNow
        };

        _db.SchemeConfigs.Add(schemeConfig);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new DomainException(ex.InnerException?.Message ?? ex.Message);
        }
    }


    public async Task CreateSchemeConfigAsync(SchemeConfigUpdatePayload p, long? authorizedBy)
    {
        var schemedata = new SchemeMaster 
        {
            //TenantId = 0,  

            TenureMonths = p.TenureMonths ?? 20,
            OrgFeePct = p.OrgFeePct ?? 5.00m,
            SifinCommissionPct = p.SifinCommissionPct ?? 1.00m,
            MinBidPct = p.MinBidPct ?? 15.00m,
            MaxBidPct = p.MaxBidPct ?? 50.00m,
            EarlyExitPenaltyPct = p.EarlyExitPenaltyPct ?? 15.00m,
            MinInstallmentsForEligibility = p.MinInstallmentsForEligibility ?? 2,
            BiddingWindowOpenDay = p.BiddingWindowOpenDay ?? 1,
            BiddingDayOfMonth = p.BiddingDayOfMonth ?? 15,
            NoBidDefaultDividendPct = p.NoBidDefaultDividendPct ?? 0.00m,

            KycMode = Enum.TryParse<KycMode>(p.KycMode, true, out var mode)
                ? mode
                : KycMode.MINIMAL_FIRST,

            MakerCheckerEnabled = p.MakerCheckerEnabled ?? true,

            // Bank Details  
            BankName = p.BankName,
            CustAddress1 = p.CustAddress1,
            CustAddress2 = p.CustAddress2,
            CustAddress3 = p.CustAddress3,
            Email = p.Email,
            PhNum = p.PhNum,
            RdStatus = p.RdStatus,

            // Bonus  
            GrossBonus = p.GrossBonus ?? 0m,
            TenantCommission = p.TenantCommission ?? 0m,
            NetBonus = p.NetBonus ?? 0m,

            // GL / Reserve  
            Reserve1 = p.Reserve1,
            Reserve2 = p.Reserve2,
            PoolMoney = p.PoolMoney,
            TenantPin = p.TenantPin,
            LoanAssetGL = p.LoanAssetGL,
            SifinPayable = p.SifinPayable,

            // Time Change  
            TimeChPass = p.TimeChPass ?? 0m,

            // Penalty  
            PenaltyAcc = p.PenaltyAcc,
            NMPenaltyAcc = p.NMPenaltyAcc,

            // Interest  
            MinimumRate = p.MinimumRate ?? 0m,
            MaximumRate = p.MaximumRate ?? 0m,
            MinimumPeriod = p.MinimumPeriod ?? 0,
            MaximumPeriod = p.MaximumPeriod ?? 0,

            // Tax  
            TdsAc = p.TdsAc,
            ServicesTax = p.ServicesTax,

            // Audit  
            UpdatedBy = authorizedBy,
            UpdatedAt = DateTime.UtcNow,
            AuthorizedBy = authorizedBy,
            AuthorizedAt = DateTime.UtcNow
        };

        _db.SchemeMaster.Add(schemedata);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new DomainException(ex.InnerException?.Message ?? ex.Message);
        }
    }

    //public async Task SetTenantStatusAsync(long tenantId, TenantStatus status, long? updatedBy)
    //{
    //    var t = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.TenantId == tenantId)
    //        ?? throw new DomainException($"Tenant {tenantId} not found");
    //    t.Status = status;
    //    t.UpdatedBy = updatedBy;
    //    t.UpdatedAt = DateTime.UtcNow;
    //    await _db.SaveChangesAsync();
    //}

    public async Task SetTenantStatusAsync(long tenantId, TenantStatus status, long? updatedBy)
    {
        var t = await _db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId)
            ?? throw new DomainException($"Tenant {tenantId} not found");

        t.Status = status;
        t.UpdatedBy = updatedBy;
        t.UpdatedAt = DateTime.UtcNow;

        // Set authorization details when the tenant becomes ACTIVE
        if (status == TenantStatus.ACTIVE)
        {
            t.AuthorizedBy = updatedBy;
            t.AuthorizedAt = DateTime.UtcNow;
        }

        // Optional: Clear authorization details if rejected
        if (status == TenantStatus.REJECTED)
        {
            t.AuthorizedBy = null;
            t.AuthorizedAt = null;
        }

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

        // Bank Details
        if (!string.IsNullOrWhiteSpace(p.BankName))
            sc.BankName = p.BankName;

        if (!string.IsNullOrWhiteSpace(p.CustAddress1))
            sc.CustAddress1 = p.CustAddress1;

        if (!string.IsNullOrWhiteSpace(p.CustAddress2))
            sc.CustAddress2 = p.CustAddress2;

        if (!string.IsNullOrWhiteSpace(p.CustAddress3))
            sc.CustAddress3 = p.CustAddress3;

        if (!string.IsNullOrWhiteSpace(p.Email))
            sc.Email = p.Email;

        if (!string.IsNullOrWhiteSpace(p.PhNum))
            sc.PhNum = p.PhNum;

        //if (!string.IsNullOrWhiteSpace(p.Fax))
        //    sc.Fax = p.Fax;

        if (!string.IsNullOrWhiteSpace(p.RdStatus))
            sc.RdStatus = p.RdStatus;

        // Bonus / Commission
        if (p.GrossBonus.HasValue)
            sc.GrossBonus = p.GrossBonus.Value;

        if (p.TenantCommission.HasValue)
            sc.TenantCommission = p.TenantCommission.Value;

        if (p.NetBonus.HasValue)
            sc.NetBonus = p.NetBonus.Value;

        // Reserve / GL
        if (!string.IsNullOrWhiteSpace(p.Reserve1))
            sc.Reserve1 = p.Reserve1;

        if (!string.IsNullOrWhiteSpace(p.Reserve2))
            sc.Reserve2 = p.Reserve2;

        if (!string.IsNullOrWhiteSpace(p.PoolMoney))
            sc.PoolMoney = p.PoolMoney;

        if (!string.IsNullOrWhiteSpace(p.TenantPin))
            sc.TenantPin = p.TenantPin;

        if (!string.IsNullOrWhiteSpace(p.LoanAssetGL))
            sc.LoanAssetGL = p.LoanAssetGL;

        if (!string.IsNullOrWhiteSpace(p.SifinPayable))
            sc.SifinPayable = p.SifinPayable;

        // Time Change
        if (p.TimeChPass.HasValue)
            sc.TimeChPass = p.TimeChPass.Value;

        // Penalty Accounts
        if (!string.IsNullOrWhiteSpace(p.PenaltyAcc))
            sc.PenaltyAcc = p.PenaltyAcc;

        if (!string.IsNullOrWhiteSpace(p.NMPenaltyAcc))
            sc.NMPenaltyAcc = p.NMPenaltyAcc;

        // Interest Configuration
        if (p.MinimumRate.HasValue)
            sc.MinimumRate = p.MinimumRate.Value;

        if (p.MaximumRate.HasValue)
            sc.MaximumRate = p.MaximumRate.Value;

        if (p.MinimumPeriod.HasValue)
            sc.MinimumPeriod = p.MinimumPeriod.Value;

        if (p.MaximumPeriod.HasValue)
            sc.MaximumPeriod = p.MaximumPeriod.Value;

        // Tax
        if (!string.IsNullOrWhiteSpace(p.TdsAc))
            sc.TdsAc = p.TdsAc;

        if (!string.IsNullOrWhiteSpace(p.ServicesTax))
            sc.ServicesTax = p.ServicesTax;

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
    public async Task<IReadOnlyList<SchemeSummaryDto>> GetAllSchemeSummariesAsync()
    {
        var data = await (
            from t in _db.Tenants.AsNoTracking()
            join s in _db.SchemeConfigs.AsNoTracking()
                on t.TenantId equals s.TenantId
            select new SchemeSummaryDto(
                t.TenantId,
                t.Name,
                t.Address,
                t.Phone,
                t.OrgEmail,

                s.TenureMonths,
                s.OrgFeePct,
                s.SifinCommissionPct,
                s.MinBidPct,
                s.MaxBidPct,
                s.EarlyExitPenaltyPct,
                s.MinInstallmentsForEligibility,
                s.BiddingWindowOpenDay,
                s.BiddingDayOfMonth,
                s.NoBidDefaultDividendPct,

                s.KycMode.ToString(),
                s.MakerCheckerEnabled,

                s.BankName,
                s.CustAddress1,
                s.CustAddress2,
                s.CustAddress3,
                s.PhNum,
                s.RdStatus,

                s.GrossBonus,
                s.TenantCommission,
                s.NetBonus,

                s.Reserve1,
                s.Reserve2,
                s.PoolMoney,
                s.TenantPin,
                s.LoanAssetGL,
                s.SifinPayable,

                s.TimeChPass,

                s.PenaltyAcc,
                s.NMPenaltyAcc,

                s.MinimumRate,
                s.MaximumRate,
                s.MinimumPeriod,
                s.MaximumPeriod,

                s.TdsAc,
                s.ServicesTax,

                s.UpdatedBy,
                s.UpdatedAt,
                s.AuthorizedBy,
                s.AuthorizedAt
            )
        ).ToListAsync();

        return data;
    
    }


   public async Task<IReadOnlyList<ProductSummaryDto>> GetAllProductSummariesAsync()
    {
        var schemes = await _db.SchemeMaster
            .AsNoTracking()
            .ToListAsync();

        return schemes.Select(s => MapToDto(s)).ToList();
    }


    public async Task<ProductSummaryDto> GetProductSummaryByIdAsync(int schemeId)
    {
        var scheme = await _db.SchemeMaster
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SchemeId == schemeId);

        if (scheme == null)
            throw new DomainException($"Scheme with ID {schemeId} not found");



        return MapToDto(scheme);
    }
    public Task<Tenant?> GetAsync(long tenantId) =>
        _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.TenantId == tenantId);

    public async Task<IReadOnlyList<Tenant>> ListAsync() =>
        await _db.Tenants.IgnoreQueryFilters().OrderBy(t => t.TenantId).ToListAsync();

    public async Task<SchemeConfig> GetSchemeConfigAsync(long tenantId) =>
        await _db.SchemeConfigs.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.TenantId == tenantId)
            ?? throw new DomainException($"Tenant {tenantId} has no scheme_config");


    public async Task<SchemeMaster> GetSchemeConfigAsync() =>
    await _db.SchemeMaster.IgnoreQueryFilters().FirstOrDefaultAsync()
        ?? throw new DomainException($"Tenant no scheme_config");

    private ProductSummaryDto MapToDto(SchemeMaster scheme)
    {
        return new ProductSummaryDto(
            (int)scheme.SchemeId,
            scheme.SchemeName,
             scheme.TenureMonths,
            scheme.OrgFeePct,
            scheme.SifinCommissionPct,
            scheme.MinBidPct,
            scheme.MaxBidPct,
            scheme.EarlyExitPenaltyPct,
            scheme.MinInstallmentsForEligibility,
            scheme.BiddingWindowOpenDay,
            scheme.BiddingDayOfMonth,
            scheme.NoBidDefaultDividendPct,
            scheme.KycMode.ToString(),
            scheme.MakerCheckerEnabled,
            scheme.BankName,
            scheme.CustAddress1,
            scheme.CustAddress2,
            scheme.CustAddress3,
            scheme.PhNum,
            scheme.RdStatus,
            scheme.GrossBonus,
            scheme.TenantCommission,
            scheme.NetBonus,
            scheme.Reserve1,
            scheme.Reserve2,
            scheme.PoolMoney,
            scheme.TenantPin,
            scheme.LoanAssetGL,
            scheme.SifinPayable,
            scheme.TimeChPass,
            scheme.PenaltyAcc,
            scheme.NMPenaltyAcc,
            scheme.MinimumRate,
            scheme.MaximumRate,
            scheme.MinimumPeriod,
            scheme.MaximumPeriod,
            scheme.TdsAc,
            scheme.ServicesTax,
            scheme.UpdatedBy,
            scheme.UpdatedAt,
            scheme.AuthorizedBy,
            scheme.AuthorizedAt
        );
    }

    public async Task<IReadOnlyList<ProductSummaryDto>> GetByProductSummariesAsync() 
    {
        var query = from s in _db.SchemeMaster.AsNoTracking()
                    select new ProductSummaryDto(
                        (int)s.SchemeId,
                        s.SchemeName,
             
                        s.TenureMonths,
                        s.OrgFeePct,
                        s.SifinCommissionPct,
                        s.MinBidPct,
                        s.MaxBidPct,
                        s.EarlyExitPenaltyPct,
                        s.MinInstallmentsForEligibility,
                        s.BiddingWindowOpenDay,
                        s.BiddingDayOfMonth,
                        s.NoBidDefaultDividendPct,
                        s.KycMode.ToString(),
                        s.MakerCheckerEnabled,
                        s.BankName,
                        s.CustAddress1,
                        s.CustAddress2,
                        s.CustAddress3,
                        s.PhNum,
                        s.RdStatus,
                        s.GrossBonus,
                        s.TenantCommission,
                        s.NetBonus,
                        s.Reserve1,
                        s.Reserve2,
                        s.PoolMoney,
                        s.TenantPin,
                        s.LoanAssetGL,
                        s.SifinPayable,
                        s.TimeChPass,
                        s.PenaltyAcc,
                        s.NMPenaltyAcc,
                        s.MinimumRate,
                        s.MaximumRate,
                        s.MinimumPeriod,
                        s.MaximumPeriod,
                        s.TdsAc,
                        s.ServicesTax,
                        s.UpdatedBy,
                        s.UpdatedAt,
                        s.AuthorizedBy,
                        s.AuthorizedAt
                    );

        var schemes = await query.ToListAsync();

        // Add tenant details if needed
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync();

        // Since records are immutable, you'll need to create new ones with tenant data
        // This is why the mapping approach is better
        return schemes;
    }

}
