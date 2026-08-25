using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System;

namespace EasyMoney.Api.Services;

public interface ITenantService
{
    Task<Tenant> CreateTenantAsync(CreateTenantRequest req, long? createdBy, long? authorizedBy, bool isSuperAdmin);
    Task SetTenantStatusAsync(long tenantId, TenantStatus status, long? updatedBy);
    Task UpdateSchemeConfigAsync(long tenantId, SchemeConfigUpdatePayload p, long? authorizedBy);
    Task UpdateProductAsync(long schemId, SchemeConfigUpdatePayload p, long? authorizedBy);  

    Task<Tenant?> GetAsync(long tenantId);
    Task<IReadOnlyList<Tenant>> ListAsync();
    Task<SchemeConfig> GetSchemeConfigAsync(long tenantId);
    Task<SchemeMaster> GetProductConfigAsync(long schemeId); 
     
    Task<SchemeMaster> GetSchemeConfigAsync();

    Task CreateSchemeConfigAsync(long tenantId, SchemeConfigUpdatePayload req, long? authorizedBy);
    Task CreateSchemeConfigAsync(SchemeConfigUpdatePayload req, long? authorizedBy);

    Task<IReadOnlyList<SchemeSummaryDto>> GetAllSchemeSummariesAsync();

    Task<IReadOnlyList<ProductSummaryDto>> GetAllProductSummariesAsync();

    Task<ProductSummaryDto> GetProductSummaryByIdAsync(int schemeId);


    //gl creation  
    //Task<GeneralLedgerMaster> GetByIdAsync(int glId);
    //Task<GeneralLedgerMaster> GetGlAsync();
    Task<IReadOnlyList<GeneralLedgerMaster>> GetAllGLSummariesAsync();
      Task<GeneralLedgerMaster> GetGlAsync();
    Task<GeneralLedgerMaster> GetGlAsync(int glId);

    Task CreateGl(GeneralLedgerDto req, long? authorizedBy);  
    Task UpdateProductAsync(int glId, GeneralLedgerMaster p, long? authorizedBy);


    //Task<GlAccount> GetTenantGLAccountAsync(long tenantId);
    Task<IReadOnlyList<GlAccount>> GetTenantGLAccountsAsync(long tenantId);
    Task<GeneralLedgerMaster> GetGLSummaryByIdAsync(int glId);

    Task<GeneralLedgerMaster> GetGldata(long glId);
    Task<GlAccount> GetGlcreateAsync();

    //Task<bool> UpdateTenantLogoAsync(long tenantId, IFormFile logoFile);
    Task<Tenant?> GetTenantByIdAsync(long tenantId);
    Task<bool> UpdateTenantLogoAsync(long tenantId, IFormFile logoFile);

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
        // Validate required fields
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new DomainException("Tenant name required");

        // Check for duplicates
        if (await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Name == req.Name))
            throw new DomainException("Tenant name already exists");

        if (!string.IsNullOrWhiteSpace(req.Phone) &&
            await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Phone == req.Phone))
            throw new DomainException("Tenant Mobile No already exists");

        // Process logo from base64
        byte[]? logoData = null;
        string? logoContentType = null;
        string? logoFileName = null;

        if (!string.IsNullOrWhiteSpace(req.Logo))
        {
            try
            {
                // Remove data URL prefix if present
                var base64Data = req.Logo;
                if (base64Data.Contains(","))
                {
                    var parts = base64Data.Split(',');
                    if (parts.Length == 2)
                    {
                        // Extract content type from data URL
                        var contentTypeMatch = System.Text.RegularExpressions.Regex.Match(
                            parts[0], @"data:(?<type>.+?);base64"
                        );
                        if (contentTypeMatch.Success)
                            logoContentType = contentTypeMatch.Groups["type"].Value;

                        base64Data = parts[1];
                    }
                }

                // Validate base64 string
                if (string.IsNullOrWhiteSpace(base64Data))
                    throw new DomainException("Invalid logo data");

                logoData = Convert.FromBase64String(base64Data);

                // Validate size (5MB)
                if (logoData.Length > 5 * 1024 * 1024)
                    throw new DomainException("Logo file size cannot exceed 5MB");

                // Validate content type
                var allowedTypes = new[] {
                "image/jpeg", "image/png", "image/gif",
                "image/webp", "image/svg+xml", "image/bmp"
            };

                if (!string.IsNullOrEmpty(logoContentType) &&
                    !allowedTypes.Contains(logoContentType.ToLower()))
                {
                    throw new DomainException("Only JPEG, PNG, GIF, WEBP, SVG, and BMP images are allowed");
                }

                // Set default content type if not detected
                if (string.IsNullOrEmpty(logoContentType))
                {
                    // Try to detect from file extension in filename
                    if (!string.IsNullOrEmpty(req.LogoFileName))
                    {
                        var ext = Path.GetExtension(req.LogoFileName).ToLower();
                        logoContentType = ext switch
                        {
                            ".jpg" or ".jpeg" => "image/jpeg",
                            ".png" => "image/png",
                            ".gif" => "image/gif",
                            ".webp" => "image/webp",
                            ".svg" => "image/svg+xml",
                            ".bmp" => "image/bmp",
                            _ => "image/png"
                        };
                    }
                    else
                    {
                        logoContentType = "image/png"; // Default
                    }
                }

                logoFileName = req.LogoFileName ?? "logo.png";
            }
            catch (FormatException)
            {
                throw new DomainException("Invalid logo image format. Please upload a valid image.");
            }
            catch (Exception ex)
            {
                throw new DomainException($"Error processing logo: {ex.Message}");
            }
        }

        // Create tenant
        var t = new Tenant
        {
            Name = req.Name.Trim(),
            RegistrationNumber = req.RegistrationNumber?.Trim(),
            Address = req.Address?.Trim(),
            Phone = req.Phone?.Trim(),
            OrgEmail = req.OrgEmail?.Trim(),
            ContactPersonName = req.ContactPersonName?.Trim(),
            ContactPersonPhone = req.ContactPersonPhone?.Trim(),
            StartDate = req.StartDate,
            EffectiveDate = req.EffectiveDate,
            Status = isSuperAdmin ? TenantStatus.ACTIVE : TenantStatus.PENDING,
            CreatedBy = createdBy,
            AuthorizedBy = isSuperAdmin ? createdBy : null,
            AuthorizedAt = isSuperAdmin ? DateTime.UtcNow : null,
            AuthorisationRequired = req.AuthorisationRequired,
            SmsNotification = req.SmsNotification,
            EmailNotification = req.EmailNotification,
            LogoData = logoData,
            LogoContentType = logoContentType,
            LogoFileName = logoFileName
        };

        _db.Tenants.Add(t);
        await _db.SaveChangesAsync();

        // Handle approval workflow for non-superadmin
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

        // Seed chart of accounts
        await _accounting.SeedTenantChartAsync(t.TenantId, createdBy);

        _log.LogInformation("Created tenant {Tid} '{Name}' with logo: {HasLogo} ({Size} bytes)",
            t.TenantId, t.Name, logoData != null, logoData?.Length ?? 0);

        return t;
    }



    public async Task<Tenant?> GetTenantByIdAsync(long tenantId)
    {
        return await _db.Tenants.FindAsync(tenantId);
    }

    public async Task<bool> UpdateTenantLogoAsync(long tenantId, IFormFile logoFile)
    {
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null)
            return false;

        if (logoFile == null || logoFile.Length == 0)
            return false;

        // Validate file size (max 5MB)
        if (logoFile.Length > 5 * 1024 * 1024)
            throw new DomainException("Logo file size cannot exceed 5MB");

        // Validate file type
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml" };
        if (!allowedTypes.Contains(logoFile.ContentType.ToLower()))
            throw new DomainException("Only JPEG, PNG, GIF, WEBP, and SVG images are allowed");

        using var memoryStream = new MemoryStream();
        await logoFile.CopyToAsync(memoryStream);

        tenant.LogoData = memoryStream.ToArray();
        tenant.LogoContentType = logoFile.ContentType;
        tenant.LogoFileName = Path.GetFileName(logoFile.FileName);
        tenant.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<byte[]?> GetTenantLogoAsync(long tenantId)
    {
        var tenant = await _db.Tenants
            .Where(t => t.TenantId == tenantId)
            .Select(t => new { t.LogoData, t.LogoContentType })
            .FirstOrDefaultAsync();

        return tenant?.LogoData;
    }

    //public async Task<Tenant> CreateTenantAsync(CreateTenantRequest req, long? createdBy, long? authorizedBy, bool isSuperAdmin)
    //{
    //    if (string.IsNullOrWhiteSpace(req.Name)) throw new DomainException("Tenant name required");
    //    if (await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Name == req.Name))
    //        throw new DomainException("Tenant name already exists");
    //     if (await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Phone == req.Phone))
    //        throw new DomainException("Tenant Mobile No already exists");
    //    var t = new Tenant
    //    {
    //        Name = req.Name,
    //        RegistrationNumber = req.RegistrationNumber,
    //        Address = req.Address,
    //        Phone = req.Phone,
    //        OrgEmail = req.OrgEmail,
    //        ContactPersonName = req.ContactPersonName,
    //        ContactPersonPhone = req.ContactPersonPhone,
    //        StartDate = req.StartDate,
    //        EffectiveDate = req.EffectiveDate,
    //        Status = isSuperAdmin ? TenantStatus.ACTIVE : TenantStatus.PENDING,
    //        CreatedBy = createdBy,
    //        AuthorizedBy = isSuperAdmin ? createdBy : null,
    //        AuthorizedAt = isSuperAdmin ? DateTime.UtcNow  : null,
    //        AuthorisationRequired = req.AuthorisationRequired ,
    //        SmsNotification = req.SmsNotification ,
    //        EmailNotification = req.EmailNotification,
    //    };
    //    _db.Tenants.Add(t);
    //    await _db.SaveChangesAsync();

    //    if (!isSuperAdmin)
    //    {
    //        if (!createdBy.HasValue)
    //            throw new DomainException("Created user required for maker-checker request");

    //        var approval = new ApprovalRequest
    //        {
    //            TenantId = t.TenantId,
    //            ActionType = ApprovalActionType.TENANT_CREATE,
    //            EntityType = "TENANT",
    //            EntityId = t.TenantId,
    //            Payload = System.Text.Json.JsonSerializer.Serialize(req),
    //            Status = ApprovalStatus.PENDING,
    //            RequestedBy = createdBy.Value,
    //            RequestedAt = DateTime.UtcNow
    //        };

    //        _db.ApprovalRequests.Add(approval);
    //        await _db.SaveChangesAsync();
    //    }

    //    // 1:1 scheme_config row (defaults baked in via Domain entity property initializers)
    //    //var sc = new SchemeConfig { TenantId = t.TenantId };
    //    //_db.SchemeConfigs.Add(sc);
    //    //await _db.SaveChangesAsync();

    //    await _accounting.SeedTenantChartAsync(t.TenantId, createdBy);

    //    _log.LogInformation("Created tenant {Tid} '{Name}' (reg={Reg}) with default scheme_config + CoA",
    //        t.TenantId, t.Name, t.RegistrationNumber);
    //    return t;
    //}

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
        //bool schemeExists = await _db.SchemeConfigs
        //    .IgnoreQueryFilters()
        //    .AnyAsync(s => s.TenantId == tenantId && s.SchemeName==p.SchemeName);

        //if (schemeExists)
        //    throw new DomainException($"Scheme configuration already exists for tenant '{p.SchemeName}'.");

        var schemeConfig = new SchemeConfig
        {
            TenantId = tenantId,
            SchemeName = p.SchemeName,
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
            GstGl = p.GstGl,
            SchemeCode = p.SchemeCode,
            SchemeId = 0,
            OrgFeeGlId=p.OrgFeeGlId,
            SifinCommissionGlId=p.SifinCommissionGlId,

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
            SchemeName=p.SchemeName,
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
            GstGl=p.GstGl,
            SchemeCode = p.SchemeCode,

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

        if (p.FixedRate.HasValue)
            sc.FixedRate = p.FixedRate.Value;
        // Tax
        if (!string.IsNullOrWhiteSpace(p.TdsAc))
            sc.TdsAc = p.TdsAc;

        if (!string.IsNullOrWhiteSpace(p.Email))
            sc.Email = p.Email;

        if (!string.IsNullOrWhiteSpace(p.SchemeName))
            sc.SchemeName = p.SchemeName;

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
    public async Task<SchemeMaster> GetProductConfigAsync(long schemeId) =>
await _db.SchemeMaster.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.SchemeId == schemeId)
    ?? throw new DomainException($"SCheme No Found");

    public async Task<SchemeMaster> GetSchemeConfigAsync() =>
await _db.SchemeMaster.IgnoreQueryFilters().FirstOrDefaultAsync()
    ?? throw new DomainException($"Tenant no scheme_config");

     
    public async Task<GeneralLedgerMaster> GetGlAsync() => 
        await _db.GeneralLedgerMaster.IgnoreQueryFilters().FirstOrDefaultAsync()
            ?? throw new DomainException($"Tenant no scheme_config");

    public async Task<GlAccount> GetGlcreateAsync() =>
        await _db.GlAccounts.IgnoreQueryFilters().FirstOrDefaultAsync()
            ?? throw new DomainException($"Tenant no scheme_config");


    public async Task<GeneralLedgerMaster> GetGlAsync(int glId) =>
    await _db.GeneralLedgerMaster.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.GlId == glId)
    ?? throw new DomainException($"SCheme No Found");

    public async Task<GeneralLedgerMaster> GetGldata(long glId) =>
            await _db.GeneralLedgerMaster.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.GlId == glId)
            ?? throw new DomainException($"SCheme No Found");

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
            scheme.AuthorizedAt,
            scheme.FixedRate, 
            scheme.Email,
            scheme.GstGl,
            scheme.SchemeCode

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
                        s.AuthorizedAt,
                        s.FixedRate,
                        s.Email,
                        s.GstGl,
                        s.SchemeCode
                    );

        var schemes = await query.ToListAsync();

        // Add tenant details if needed
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync();

        // Since records are immutable, you'll need to create new ones with tenant data
        // This is why the mapping approach is better
        return schemes;
    }


    public async Task UpdateProductAsync(long schmeId, SchemeConfigUpdatePayload p, long? authorizedBy) 
    {
        var sc = await _db.SchemeMaster.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.SchemeId == schmeId)
            ?? throw new DomainException($"Tenant {schmeId} has no scheme_config");

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
        if (!string.IsNullOrWhiteSpace(p.SchemeName))
            sc.SchemeName = p.SchemeName;
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

        if (!string.IsNullOrWhiteSpace(p.GstGl))
            sc.GstGl = p.GstGl;

        if (!string.IsNullOrWhiteSpace(p.SchemeCode)) 
            sc.SchemeCode = p.SchemeCode;
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

        if (p.FixedRate.HasValue)
            sc.FixedRate = p.FixedRate.Value;
        if (!string.IsNullOrWhiteSpace(p.Email))
            sc.Email = p.Email;

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

    public async Task<IReadOnlyList<GeneralLedgerMaster>> GetAllGLSummariesAsync()
    {
        var gl = await _db.GeneralLedgerMaster.AsNoTracking().ToListAsync();

        var glDtos = gl.Select(g => new GeneralLedgerMaster
        {
            //GlId = g.GlId,
            Code = g.Code,
            Name = g.Name,
            Description = g.Description,
            Forbank = g.Forbank,
            Category = g.Category,
            IsReported = g.IsReported,
            HasTransactions = g.HasTransactions,
            HasGst = g.HasGst,
            CreatedAt = g.CreatedAt,
            CreatedBy = g.CreatedBy,
            UpdatedAt = g.UpdatedAt,
            UpdatedBy = g.UpdatedBy,
            AuthorizedAt = g.AuthorizedAt,
            AuthorizedBy = g.AuthorizedBy,
            status = g.status,
            ParentGl = g.ParentGl,
            Type = g.Type
        }).ToList();

        return (IReadOnlyList<GeneralLedgerMaster>)gl;
    }
    public async Task<IReadOnlyList<GlAccount>> GetTenantGLAccountsAsync(long tenantId)
    {
        var glAccounts = await _db.GlAccounts.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .ToListAsync();

        if (glAccounts == null || !glAccounts.Any())
            throw new DomainException($"No general ledger accounts found for TenantId {tenantId}");

        return glAccounts;
    }
    public async Task<GeneralLedgerMaster> GetGLSummaryByIdAsync(int glId)
    {
        var gl = await _db.GeneralLedgerMaster.AsNoTracking()
            .FirstOrDefaultAsync(s => s.GlId == glId);

        if (gl == null)
            throw new DomainException($"General_ledger with ID {glId} not found");

        return gl; // Corrected to return the GeneralLedgerMaster entity directly  
    }

    public async Task CreateGl(GeneralLedgerDto p, long? authorizedBy) 
    {
        string prefix = GetCategoryPrefix(p.Category);

        // Get the next running number for this category
        int nextNumber = await GetNextRunningNumber(p.Category);

        // Generate the code (e.g., "1001", "2001", etc.)
        string generatedCode = $"{prefix}{nextNumber:D3}";

        var schemedata = new GeneralLedgerMaster
        {
            Code = generatedCode,
            Name = p.Name,
            ParentGl = p.ParentGl,
            Description= p.Description,
            //AccountClass = GlAccountClass.LIABILITY??"",
            //AccountClass = p.AccountClass ?? GlAccountClass.LIABILITY.ToString(),
            //AccountClass = Enum.TryParse<GlAccountClass>(p.AccountClass, true, out var accountClass) ? accountClass : GlAccountClass.ASSET,
            Category = p.Category,
            Forbank = p.Forbank ?? false,
            IsReported = p.IsReported ?? false,
            HasTransactions = p.HasTransactions ?? false,
            HasGst = p.HasGst ?? false,
            AuthorizedBy = authorizedBy,
            AuthorizedAt = DateTime.UtcNow,
            CreatedBy = authorizedBy,
            status=p.Status,
            //TenantId = p.TenantId,

        };

        _db.GeneralLedgerMaster.Add(schemedata);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new DomainException(ex.InnerException?.Message ?? ex.Message);
        }
    }

    public async Task UpdateProductAsync(int glId, GeneralLedgerMaster p, long? authorizedBy)
    {
        
    var sc = await _db.GeneralLedgerMaster.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.GlId == glId)
            ?? throw new DomainException($"General Ledger {glId} has no Gl_config");

        if (!string.IsNullOrWhiteSpace(p.Code)) sc.Code = p.Code;
        if (!string.IsNullOrWhiteSpace(p.Name)) sc.Name = p.Name;
        if (p.Forbank.HasValue) sc.Forbank = p.Forbank.Value;
        if (p.IsReported.HasValue) sc.IsReported = p.IsReported.Value;
        if (p.HasTransactions.HasValue) sc.HasTransactions = p.HasTransactions.Value;
        if (p.HasGst.HasValue) sc.HasGst = p.HasGst.Value;
        sc.status = p.status ;
        sc.ParentGl = p.ParentGl;
        sc.Category = p.Category;
        sc.Description = p.Description;
        //sc.Category = Enum.TryParse<GlAccountClass>(p.AccountClass.ToString(), true, out var accountClass)
        //    ? accountClass
        //    : sc.AccountClass;


        sc.AuthorizedBy = authorizedBy;
        sc.AuthorizedAt = authorizedBy.HasValue ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync();
    }

    private string GetCategoryPrefix(string category)
    {
        return category?.ToUpper() switch
        {
            "ASSET" => "1",
            "LIABILITY" => "2",
            "INCOME" => "3",
            "EXPENSE" => "4",
            "EQUITY" => "5",
            _ => throw new ArgumentException($"Invalid category: {category}")
        };
    }

    private async Task<int> GetNextRunningNumber(string category)
    {
        // Get the prefix for this category
        string prefix = GetCategoryPrefix(category);

        // Find the maximum code for this category
        var maxCode = await _db.GeneralLedgerMaster
            .Where(g => g.Category == category && g.Code.StartsWith(prefix))
            .Select(g => g.Code)
            .OrderByDescending(c => c)
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(maxCode))
        {
            // No existing records for this category, start from 1
            return 1;
        }

        // Extract the numeric part (after the prefix)
        string numberPart = maxCode.Substring(prefix.Length);
        if (int.TryParse(numberPart, out int currentMax))
        {
            return currentMax + 1;
        }

        // If parsing fails, start from 1
        return 1;
    }
}
