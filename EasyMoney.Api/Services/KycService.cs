using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EasyMoney.Api.Services;

public interface IKycService
{
    // Detail upserts
    Task UpsertIndividualDetailAsync(long memberId, UpsertIndividualKycRequest req);
    Task UpsertCorporateDetailAsync(long memberId, UpsertCorporateKycRequest req);

    // Tier
    Task PromoteToFullAsync(long memberId);

    // Reviews (state transitions)
    Task<KycReview> ReviewAsync(long memberId, KycStatus toStatus, string? remarks);
    Task<IReadOnlyList<KycReviewDto>> GetReviewHistoryAsync(long memberId);

    // Helpers
    Task<IndividualKycDetail?> GetIndividualDetailAsync(long memberId);
    Task<CorporateKycDetail?> GetCorporateDetailAsync(long memberId);
}

public class KycService : IKycService
{
    private readonly EasyMoneyDbContext _db;
    private readonly ITenantContext _ctx;
    private readonly ILogger<KycService> _log;

    public KycService(EasyMoneyDbContext db, ITenantContext ctx, ILogger<KycService> log)
    {
        _db = db; _ctx = ctx; _log = log;
    }

    // ============================================================
    // Individual / Corporate detail upserts
    // ============================================================
    //public async Task UpsertIndividualDetailAsync(long memberId, UpsertIndividualKycRequest req)
    //{
    //    var m = await _db.Members.FirstOrDefaultAsync(x => x.MemberId == memberId)
    //        ?? throw new DomainException($"Member {memberId} not found");
    //    if (m.MemberType != MemberType.INDIVIDUAL)
    //        throw new DomainException("Individual KYC detail only applies to INDIVIDUAL members");

    //    var detail = await _db.IndividualKycDetails.FirstOrDefaultAsync(x => x.MemberId == memberId);
    //    if (detail is null)
    //    {
    //        detail = new IndividualKycDetail { MemberId = memberId };
    //        _db.IndividualKycDetails.Add(detail);
    //    }
    //    detail.DateOfBirth = req.DateOfBirth ?? detail.DateOfBirth;
    //    detail.Gender = req.Gender ?? detail.Gender;
    //    detail.FatherOrSpouseName = req.FatherOrSpouseName ?? detail.FatherOrSpouseName;
    //    detail.PanNumber = req.PanNumber ?? detail.PanNumber;
    //    detail.AadhaarNumber = req.AadhaarNumber ?? detail.AadhaarNumber;
    //    detail.AadhaarLast4 = req.AadhaarLast4 ?? detail.AadhaarLast4
    //        ?? (req.AadhaarNumber is { Length: 12 } a ? a[^4..] : null);
    //    detail.Occupation = req.Occupation ?? detail.Occupation;
    //    detail.AnnualIncomeBand = req.AnnualIncomeBand ?? detail.AnnualIncomeBand;
    //    detail.NomineeName = req.NomineeName ?? detail.NomineeName;
    //    detail.NomineeRelation = req.NomineeRelation ?? detail.NomineeRelation;
    //    detail.NomineeDob = req.NomineeDob ?? detail.NomineeDob;
    //    detail.PermanentAddressLine = req.PermanentAddressLine ?? detail.PermanentAddressLine;
    //    detail.PermanentCity = req.PermanentCity ?? detail.PermanentCity;
    //    detail.PermanentState = req.PermanentState ?? detail.PermanentState;
    //    detail.PermanentPincode = req.PermanentPincode ?? detail.PermanentPincode;
    //    detail.UpdatedAt = DateTime.UtcNow;
    //    await _db.SaveChangesAsync();
    //}


    public async Task UpsertIndividualDetailAsync(long memberId, UpsertIndividualKycRequest req)
    {
        var m = await _db.Members.FirstOrDefaultAsync(x => x.MemberId == memberId)
            ?? throw new DomainException($"Member {memberId} not found");

        if (m.MemberType != MemberType.INDIVIDUAL)
            throw new DomainException("Individual KYC detail only applies to INDIVIDUAL members");

        var detail = await _db.IndividualKycDetails.FirstOrDefaultAsync(x => x.MemberId == memberId);

        if (detail == null)
        {
            detail = new IndividualKycDetail
            {
                MemberId = memberId
            };

            _db.IndividualKycDetails.Add(detail);
        }

        // Personal Details
        detail.CustomerId = req.CustomerId ?? detail.CustomerId;
        detail.Name = req.Name ?? detail.Name;
        detail.FatherOrSpouseName = req.FatherOrSpouseName ?? detail.FatherOrSpouseName;
        detail.Address = req.Address ?? detail.Address;
        detail.MobileNumber = req.MobileNumber ?? detail.MobileNumber;
        detail.ResidencePhone = req.ResidencePhone ?? detail.ResidencePhone;
        detail.OfficePhone = req.OfficePhone ?? detail.OfficePhone;
        detail.Email = req.Email ?? detail.Email;
        detail.DateOfBirth = req.DateOfBirth ?? detail.DateOfBirth;
        detail.Age = req.Age ?? detail.Age;
        detail.Education = req.Education ?? detail.Education;
        detail.MaritalStatus = req.MaritalStatus ?? detail.MaritalStatus;

        // ID Details
        detail.IdType = req.IdType ?? detail.IdType;
        detail.IdNumber = req.IdNumber ?? detail.IdNumber;
        detail.AddressProof = req.AddressProof ?? detail.AddressProof;
        detail.DocumentNumber = req.DocumentNumber ?? detail.DocumentNumber;
        detail.CustomerImage = req.CustomerImage ?? detail.CustomerImage;
        detail.IdImage = req.IdImage ?? detail.IdImage;
        detail.DocumentImage = req.DocumentImage ?? detail.DocumentImage;

        // Member Details
        detail.MembershipNumber = req.MembershipNumber ?? detail.MembershipNumber;
        detail.AccountType = req.AccountType ?? detail.AccountType;
        detail.AccountNumber = req.AccountNumber ?? detail.AccountNumber;

        // Other Bank Details
        detail.BankName = req.BankName ?? detail.BankName;
        detail.SavingsAccountNumber = req.SavingsAccountNumber ?? detail.SavingsAccountNumber;
        detail.CurrentAccountNumber = req.CurrentAccountNumber ?? detail.CurrentAccountNumber;

        // KYC
        detail.Gender = req.Gender ?? detail.Gender;
        detail.PanNumber = req.PanNumber ?? detail.PanNumber;
        detail.AadhaarNumber = req.AadhaarNumber ?? detail.AadhaarNumber;
        detail.AadhaarLast4 = req.AadhaarLast4
            ?? detail.AadhaarLast4
            ?? (req.AadhaarNumber is { Length: 12 } a ? a[^4..] : null);

        detail.Occupation = req.Occupation ?? detail.Occupation;
        detail.AnnualIncomeBand = req.AnnualIncomeBand ?? detail.AnnualIncomeBand;
        detail.NomineeName = req.NomineeName ?? detail.NomineeName;
        detail.NomineeRelation = req.NomineeRelation ?? detail.NomineeRelation;
        detail.NomineeDob = req.NomineeDob ?? detail.NomineeDob;
        detail.PermanentAddressLine = req.PermanentAddressLine ?? detail.PermanentAddressLine;
        detail.PermanentCity = req.PermanentCity ?? detail.PermanentCity;
        detail.PermanentState = req.PermanentState ?? detail.PermanentState;
        detail.PermanentPincode = req.PermanentPincode ?? detail.PermanentPincode;

        // Family Details
        detail.TotalFamilyMembers = req.TotalFamilyMembers ?? detail.TotalFamilyMembers;
        detail.DependentFamilyMembers = req.DependentFamilyMembers ?? detail.DependentFamilyMembers;
        detail.EarningFamilyMembers = req.EarningFamilyMembers ?? detail.EarningFamilyMembers;
        detail.AdditionalPersonalDetails = req.AdditionalPersonalDetails ?? detail.AdditionalPersonalDetails;
        detail.Remarks = req.Remarks ?? detail.Remarks;
        detail.Remarks1 = req.Remarks1 ?? detail.Remarks1;

        // Vehicle Details
        detail.BikeModel = req.BikeModel ?? detail.BikeModel;
        detail.BikeCompany = req.BikeCompany ?? detail.BikeCompany;
        detail.CarModel = req.CarModel ?? detail.CarModel;
        detail.CarCompany = req.CarCompany ?? detail.CarCompany;
        detail.TractorModel = req.TractorModel ?? detail.TractorModel;
        detail.TractorCompany = req.TractorCompany ?? detail.TractorCompany;
        detail.HeavyVehicleModel = req.HeavyVehicleModel ?? detail.HeavyVehicleModel;
        detail.HeavyVehicleCompany = req.HeavyVehicleCompany ?? detail.HeavyVehicleCompany;

        // Property Details
        detail.AgricultureLand = req.AgricultureLand ?? detail.AgricultureLand;
        detail.AgricultureArea = req.AgricultureArea ?? detail.AgricultureArea;
        detail.AgricultureSurveyNo = req.AgricultureSurveyNo ?? detail.AgricultureSurveyNo;
        detail.AgricultureValue = req.AgricultureValue ?? detail.AgricultureValue;

        detail.SiteDetails = req.SiteDetails ?? detail.SiteDetails;
        detail.SiteArea = req.SiteArea ?? detail.SiteArea;
        detail.SiteSurveyNo = req.SiteSurveyNo ?? detail.SiteSurveyNo;
        detail.SiteValue = req.SiteValue ?? detail.SiteValue;

        detail.PlantationDetails = req.PlantationDetails ?? detail.PlantationDetails;
        detail.PlantationArea = req.PlantationArea ?? detail.PlantationArea;
        detail.PlantationSurveyNo = req.PlantationSurveyNo ?? detail.PlantationSurveyNo;
        detail.PlantationValue = req.PlantationValue ?? detail.PlantationValue;

        detail.HouseDetails = req.HouseDetails ?? detail.HouseDetails;
        detail.HouseArea = req.HouseArea ?? detail.HouseArea;
        detail.HouseNumber = req.HouseNumber ?? detail.HouseNumber;
        detail.HouseValue = req.HouseValue ?? detail.HouseValue;

        // Occupational Details
        detail.EmploymentNature = req.EmploymentNature ?? detail.EmploymentNature;
        detail.EmployerName = req.EmployerName ?? detail.EmployerName;
        detail.SalaryDetails = req.SalaryDetails ?? detail.SalaryDetails;
        detail.Designation = req.Designation ?? detail.Designation;
        detail.OrganizationNature = req.OrganizationNature ?? detail.OrganizationNature;
        detail.Department = req.Department ?? detail.Department;
        detail.OfficeAddress = req.OfficeAddress ?? detail.OfficeAddress;

        detail.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task UpsertCorporateDetailAsync(long memberId, UpsertCorporateKycRequest req)
    {
        var m = await _db.Members.FirstOrDefaultAsync(x => x.MemberId == memberId)
            ?? throw new DomainException($"Member {memberId} not found");
        if (m.MemberType != MemberType.CORPORATE)
            throw new DomainException("Corporate KYC detail only applies to CORPORATE members");

        var detail = await _db.CorporateKycDetails.FirstOrDefaultAsync(x => x.MemberId == memberId);
        if (detail is null)
        {
            detail = new CorporateKycDetail { MemberId = memberId };
            _db.CorporateKycDetails.Add(detail);
        }
        //detail.EntityType = req.EntityType ?? detail.EntityType;
        //detail.CinOrRegistrationNo = req.CinOrRegistrationNo ?? detail.CinOrRegistrationNo;
        //detail.PanNumber = req.PanNumber ?? detail.PanNumber;
        //detail.Gstin = req.Gstin ?? detail.Gstin;
        //detail.DateOfIncorporation = req.DateOfIncorporation ?? detail.DateOfIncorporation;
        //detail.RegisteredAddressLine = req.RegisteredAddressLine ?? detail.RegisteredAddressLine;
        //detail.RegisteredCity = req.RegisteredCity ?? detail.RegisteredCity;
        //detail.RegisteredState = req.RegisteredState ?? detail.RegisteredState;
        //detail.RegisteredPincode = req.RegisteredPincode ?? detail.RegisteredPincode;
        //detail.AuthorizedSignatoryName = req.AuthorizedSignatoryName ?? detail.AuthorizedSignatoryName;
        //detail.AuthorizedSignatoryDesignation = req.AuthorizedSignatoryDesignation ?? detail.AuthorizedSignatoryDesignation;
        //detail.AuthorizedSignatoryPan = req.AuthorizedSignatoryPan ?? detail.AuthorizedSignatoryPan;
        //detail.AuthorizedSignatoryAadhaarLast4 = req.AuthorizedSignatoryAadhaarLast4 ?? detail.AuthorizedSignatoryAadhaarLast4;

        // Company Details
        detail.EntityName = req.EntityName ?? detail.EntityName;
        detail.EntityType = req.EntityType ?? detail.EntityType;
        detail.CinOrRegistrationNo = req.CinOrRegistrationNo ?? detail.CinOrRegistrationNo;
        detail.PanNumber = req.PanNumber ?? detail.PanNumber;
        detail.Gstin = req.Gstin ?? detail.Gstin;
        detail.DateOfIncorporation = req.DateOfIncorporation ?? detail.DateOfIncorporation;
        detail.PlaceOfIncorporation = req.PlaceOfIncorporation ?? detail.PlaceOfIncorporation;
        detail.CountryOfIncorporation = req.CountryOfIncorporation ?? detail.CountryOfIncorporation;

        // Registered Address
        detail.RegisteredAddressLine = req.RegisteredAddressLine ?? detail.RegisteredAddressLine;
        detail.RegisteredCity = req.RegisteredCity ?? detail.RegisteredCity;
        detail.RegisteredState = req.RegisteredState ?? detail.RegisteredState;
        detail.RegisteredPincode = req.RegisteredPincode ?? detail.RegisteredPincode;

        // Contact Details
        detail.PhoneNumber = req.PhoneNumber ?? detail.PhoneNumber;
        detail.Email = req.Email ?? detail.Email;
        detail.Website = req.Website ?? detail.Website;

        // Director
        detail.DirectorName = req.DirectorName ?? detail.DirectorName;
        detail.DirectorDesignation = req.DirectorDesignation ?? detail.DirectorDesignation;
        detail.DirectorDin = req.DirectorDin ?? detail.DirectorDin;
        detail.DirectorPan = req.DirectorPan ?? detail.DirectorPan;
        detail.DirectorDateOfBirth = req.DirectorDateOfBirth ?? detail.DirectorDateOfBirth;

        // Beneficial Owner
        detail.BeneficialOwnerName = req.BeneficialOwnerName ?? detail.BeneficialOwnerName;
        detail.OwnershipPercentage = req.OwnershipPercentage ?? detail.OwnershipPercentage;
        detail.BeneficialOwnerPan = req.BeneficialOwnerPan ?? detail.BeneficialOwnerPan;
        detail.BeneficialOwnerDin = req.BeneficialOwnerDin ?? detail.BeneficialOwnerDin;
        detail.Nationality = req.Nationality ?? detail.Nationality;
        detail.BeneficialOwnerAddress = req.BeneficialOwnerAddress ?? detail.BeneficialOwnerAddress;

        // Authorized Signatory
        detail.AuthorizedSignatoryName = req.AuthorizedSignatoryName ?? detail.AuthorizedSignatoryName;
        detail.AuthorizedSignatoryDesignation = req.AuthorizedSignatoryDesignation ?? detail.AuthorizedSignatoryDesignation;
        detail.AuthorizedSignatoryPan = req.AuthorizedSignatoryPan ?? detail.AuthorizedSignatoryPan;
        detail.AuthorizedSignatoryDin = req.AuthorizedSignatoryDin ?? detail.AuthorizedSignatoryDin;
        detail.AuthorizedSignatoryEmail = req.AuthorizedSignatoryEmail ?? detail.AuthorizedSignatoryEmail;
        detail.AuthorizedSignatoryPhoneNumber = req.AuthorizedSignatoryPhoneNumber ?? detail.AuthorizedSignatoryPhoneNumber;
        detail.AuthorizedSignatoryAadhaarLast4 = req.AuthorizedSignatoryAadhaarLast4 ?? detail.AuthorizedSignatoryAadhaarLast4;
        detail.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public Task<IndividualKycDetail?> GetIndividualDetailAsync(long memberId) =>
        _db.IndividualKycDetails.FirstOrDefaultAsync(x => x.MemberId == memberId);

    public Task<CorporateKycDetail?> GetCorporateDetailAsync(long memberId) =>
        _db.CorporateKycDetails.FirstOrDefaultAsync(x => x.MemberId == memberId);

    // ============================================================
    // Tier promotion: MINIMAL -> FULL
    // Requires: detail row exists, all mandatory docs for member_type are VERIFIED.
    // ============================================================
    public async Task PromoteToFullAsync(long memberId)
    {
        var m = await _db.Members.FirstOrDefaultAsync(x => x.MemberId == memberId)
            ?? throw new DomainException($"Member {memberId} not found");
        if (m.KycTier == KycTier.FULL) return;

        if (m.MemberType == MemberType.INDIVIDUAL)
        {
            var detail = await _db.IndividualKycDetails.FirstOrDefaultAsync(x => x.MemberId == memberId);
            if (detail is null || string.IsNullOrEmpty(detail.PanNumber))
                throw new DomainException("Individual full-KYC detail (with at least PAN) is required");
        }
        else
        {
            var detail = await _db.CorporateKycDetails.FirstOrDefaultAsync(x => x.MemberId == memberId);
            if (detail is null || string.IsNullOrEmpty(detail.PanNumber))
                throw new DomainException("Corporate full-KYC detail (with at least PAN) is required");
        }

        // Verified docs check
        var verifiedDocTypes = await _db.KycDocuments
            .Where(d => d.MemberId == memberId && d.Status == KycDocStatus.VERIFIED)
            .Select(d => d.DocType)
            .Distinct()
            .ToListAsync();

        var required = RequiredDocTypesFor(m.MemberType);
        var missing = required.Except(verifiedDocTypes).ToList();
        if (missing.Count > 0)
            throw new DomainException("Missing verified documents: " + string.Join(", ", missing));

        m.KycTier = KycTier.FULL;
        await _db.SaveChangesAsync();
    }

    public static IReadOnlyList<KycDocType> RequiredDocTypesFor(MemberType t) => t switch
    {
        MemberType.INDIVIDUAL => new[]
        {
            KycDocType.PAN, KycDocType.AADHAAR, KycDocType.ADDRESS_PROOF,
            KycDocType.PHOTO, KycDocType.BANK_PROOF
        },
        MemberType.CORPORATE => new[]
        {
            KycDocType.COMPANY_PAN, KycDocType.COMPANY_CIN, KycDocType.GST_CERTIFICATE,
            KycDocType.BOARD_RESOLUTION, KycDocType.BANK_PROOF
        },
        _ => Array.Empty<KycDocType>()
    };

    // ============================================================
    // KYC review (state transitions)
    //
    // State machine:
    //   PENDING         -> UNDER_REVIEW   (docs uploaded)
    //   UNDER_REVIEW    -> APPROVED       (staff approves; requires verified docs at FULL tier)
    //   UNDER_REVIEW    -> REJECTED       (staff rejects; remarks required)
    //   REJECTED        -> UNDER_REVIEW   (member re-submits)
    //   APPROVED        -> RE_KYC_REQUIRED (periodic refresh, or identity edit)
    //   RE_KYC_REQUIRED -> UNDER_REVIEW
    // ============================================================
    public async Task<KycReview> ReviewAsync(long memberId, KycStatus toStatus, string? remarks)
    {
        var m = await _db.Members.FirstOrDefaultAsync(x => x.MemberId == memberId)
            ?? throw new DomainException($"Member {memberId} not found");

        // 1. Validate transition
        if (!IsValidTransition(m.KycStatus, toStatus))
            throw new DomainException($"Invalid KYC transition: {m.KycStatus} -> {toStatus}");

        // 2. Approving APPROVED has extra rules:
        //    - If tier is FULL, must already pass PromoteToFullAsync's preconditions.
        //    - If tier is MINIMAL, allowed only if scheme_config.kyc_mode = MINIMAL_FIRST.
        if (toStatus == KycStatus.APPROVED)
        {
            var scheme = await _db.SchemeConfigs.IgnoreQueryFilters()
                .FirstAsync(s => s.TenantId == m.TenantId);
            if (m.KycTier == KycTier.MINIMAL && scheme.KycMode == KycMode.FULL_ONLY)
                throw new DomainException("Tenant policy (FULL_ONLY) requires kyc_tier=FULL before APPROVED");
            if (m.KycTier == KycTier.FULL)
            {
                // Re-validate that all required docs are VERIFIED at approval time.
                var verifiedDocTypes = await _db.KycDocuments
                    .Where(d => d.MemberId == memberId && d.Status == KycDocStatus.VERIFIED)
                    .Select(d => d.DocType).Distinct().ToListAsync();
                var required = RequiredDocTypesFor(m.MemberType);
                var missing = required.Except(verifiedDocTypes).ToList();
                if (missing.Count > 0)
                    throw new DomainException("Cannot approve FULL-tier KYC; missing verified docs: " + string.Join(", ", missing));
            }
        }

        // 3. REJECTED requires remarks
        if (toStatus == KycStatus.REJECTED && string.IsNullOrWhiteSpace(remarks))
            throw new DomainException("Rejection remarks are required");

        var reviewerId = _ctx.UserId
            ?? throw new DomainException("Authenticated user required to review KYC");

        // 4. Write the review row + update member.
        var review = new KycReview
        {
            TenantId = m.TenantId,
            MemberId = memberId,
            FromStatus = m.KycStatus,
            ToStatus = toStatus,
            Remarks = remarks,
            ReviewedBy = reviewerId,
            ReviewedAt = DateTime.UtcNow
        };
        _db.KycReviews.Add(review);

        m.KycStatus = toStatus;
        if (toStatus == KycStatus.APPROVED)
        {
            m.KycApprovedAt = DateTime.UtcNow;
            m.KycApprovedBy = reviewerId;
        }
        else if (toStatus == KycStatus.REJECTED || toStatus == KycStatus.RE_KYC_REQUIRED)
        {
            // Clear the approval stamp so downstream guards (open account / disbursement) re-trip.
            m.KycApprovedAt = null;
            m.KycApprovedBy = null;
        }

        await _db.SaveChangesAsync();
        _log.LogInformation("Member {Mid} KYC {From} -> {To} by user {Uid}",
            memberId, review.FromStatus, toStatus, reviewerId);
        return review;
    }

    public async Task<IReadOnlyList<KycReviewDto>> GetReviewHistoryAsync(long memberId)
    {
        return await _db.KycReviews
            .Where(r => r.MemberId == memberId)
            .OrderByDescending(r => r.ReviewedAt)
            .Select(r => new KycReviewDto(
                r.ReviewId, r.MemberId,
                r.FromStatus.ToString(), r.ToStatus.ToString(),
                r.Remarks, r.ReviewedBy, r.ReviewedAt))
            .ToListAsync();
    }

    private static bool IsValidTransition(KycStatus from, KycStatus to) => (from, to) switch
    {
        (KycStatus.PENDING, KycStatus.UNDER_REVIEW)       => true,
        (KycStatus.UNDER_REVIEW, KycStatus.APPROVED)      => true,
        (KycStatus.UNDER_REVIEW, KycStatus.REJECTED)      => true,
        (KycStatus.REJECTED, KycStatus.UNDER_REVIEW)      => true,
        (KycStatus.APPROVED, KycStatus.RE_KYC_REQUIRED)   => true,
        (KycStatus.RE_KYC_REQUIRED, KycStatus.UNDER_REVIEW) => true,
        // SIFIN-level admin override: allow any state to PENDING (resets the workflow).
        (_, KycStatus.PENDING) => true,
        _ => false
    };
}
