using EasyMoney.Api.Domain;
using Microsoft.AspNetCore.Http;
namespace EasyMoney.Api.Dtos;

// ============================================================
// Member CRUD
// ============================================================
public record CreateMemberRequest(
    MemberType MemberType,
    string FullName,
    string Phone,
    string? Email,
    string? PanNumber,
    string? AddressLine,
    string? City,
    string? State,
    string? Pincode,
    string? BankAccountNo,
    string? BankIfsc,
    string? BankHolderName,


     // Personal Details
    string? CustomerId,
    string? Name,
    string? Address,
    string? MobileNumber,
    string? ResidencePhone,
    string? OfficePhone,
    //string? Email,
    int? Age,
    string? Education,
    string? MaritalStatus,

    // ID Details
    string? IdType,
    string? IdNumber,
    string? AddressProof,
    string? DocumentNumber,
    string? CustomerImage,
    string? IdImage,
    string? DocumentImage,
    

    // Member Details
    //string? MembershipNumber,
    //string? AccountType,
    //string? AccountNumber,

    // Other Bank Details
    string? BankName,
    string? SavingsAccountNumber,
    string? CurrentAccountNumber,

    // Family Details
    int? TotalFamilyMembers,
    int? DependentFamilyMembers,
    int? EarningFamilyMembers,
    string? AdditionalPersonalDetails,
    string? Remarks,
    string? Remarks1,


    // Vehicle Details
    string? BikeModel,
    string? BikeCompany,
    string? CarModel,
    string? CarCompany,
    string? TractorModel,
    string? TractorCompany,
    string? HeavyVehicleModel,
    string? HeavyVehicleCompany,

    // Property Details
    string? AgricultureLand,
    decimal? AgricultureArea,
    string? AgricultureSurveyNo,
    decimal? AgricultureValue,

    string? SiteDetails,
    decimal? SiteArea,
    string? SiteSurveyNo,
    decimal? SiteValue,

    string? PlantationDetails,
    decimal? PlantationArea,
    string? PlantationSurveyNo,
    decimal? PlantationValue,

    string? HouseDetails,
    decimal? HouseArea,
    string? HouseNumber,
    decimal? HouseValue,

    // Occupational Details
    string? EmploymentNature,
    string? EmployerName,
    decimal? SalaryDetails,
    string? Designation,
    string? OrganizationNature,
    string? Department,
    string? OfficeAddress);

public record UpdateMemberRequest(
    string? FullName,
    string? Phone,
    string? Email,
    string? PanNumber,
    string? AddressLine,
    string? City,
    string? State,
    string? Pincode,
    string? BankAccountNo,
    string? BankIfsc,
    string? BankHolderName);

public record MemberDto(
    long MemberId,
    long TenantId,
    string MemberType,
    string FullName,
    string Phone,
    string? Email,
    string? PanNumber,
    string? AddressLine,
    string? City,
    string? State,
    string? Pincode,
    string KycTier,
    string KycStatus,
    DateTime? KycApprovedAt,
    long? KycApprovedBy,
    string? BankAccountNo,
    string? BankIfsc,
    string? BankHolderName,
    DateTime CreatedAt
    );

// ============================================================
// Individual / Corporate full KYC detail
// ============================================================
//public record UpsertIndividualKycRequest(
//    DateOnly? DateOfBirth,
//    Gender? Gender,
//    string? FatherOrSpouseName,
//    string? PanNumber,
//    string? AadhaarNumber,
//    string? AadhaarLast4,
//    string? Occupation,
//    IncomeBand? AnnualIncomeBand,
//    string? NomineeName,
//    string? NomineeRelation,
//    DateOnly? NomineeDob,
//    string? PermanentAddressLine,
//    string? PermanentCity,
//    string? PermanentState,
//    string? PermanentPincode);

public record UpsertIndividualKycRequest(
    // Personal Details
    string? CustomerId,
    string? Name,
    string? FatherOrSpouseName,
    string? Address,
    string? MobileNumber,
    string? ResidencePhone,
    string? OfficePhone,
    string? Email,
    DateOnly? DateOfBirth,
    int? Age,
    string? Education,
    string? MaritalStatus,

    // ID Details
    string? IdType,
    string? IdNumber,
    string? AddressProof,
    string? DocumentNumber,
    string? CustomerImage,
    string? IdImage,
    string? DocumentImage,

    // Member Details
    string? MembershipNumber,
    string? AccountType,
    string? AccountNumber,

    // Other Bank Details
    string? BankName,
    string? SavingsAccountNumber,
    string? CurrentAccountNumber,

    // KYC
    Gender? Gender,
    string? PanNumber,
    string? AadhaarNumber,
    string? AadhaarLast4,
    string? Occupation,
    IncomeBand? AnnualIncomeBand,
    string? NomineeName,
    string? NomineeRelation,
    DateOnly? NomineeDob,
    string? PermanentAddressLine,
    string? PermanentCity,
    string? PermanentState,
    string? PermanentPincode,

    // Family Details
    int? TotalFamilyMembers,
    int? DependentFamilyMembers,
    int? EarningFamilyMembers,
    string? AdditionalPersonalDetails,
    string? Remarks,
    string? Remarks1,

    // Vehicle Details
    string? BikeModel,
    string? BikeCompany,
    string? CarModel,
    string? CarCompany,
    string? TractorModel,
    string? TractorCompany,
    string? HeavyVehicleModel,
    string? HeavyVehicleCompany,

    // Property Details
    string? AgricultureLand,
    decimal? AgricultureArea,
    string? AgricultureSurveyNo,
    decimal? AgricultureValue,

    string? SiteDetails,
    decimal? SiteArea,
    string? SiteSurveyNo,
    decimal? SiteValue,

    string? PlantationDetails,
    decimal? PlantationArea,
    string? PlantationSurveyNo,
    decimal? PlantationValue,

    string? HouseDetails,
    decimal? HouseArea,
    string? HouseNumber,
    decimal? HouseValue,

    // Occupational Details
    string? EmploymentNature,
    string? EmployerName,
    decimal? SalaryDetails,
    string? Designation,
    string? OrganizationNature,
    string? Department,
    string? OfficeAddress
);


//public record UpsertCorporateKycRequest(
//    CorporateEntityType? EntityType,
//    string? CinOrRegistrationNo,
//    string? PanNumber,
//    string? Gstin,
//    DateOnly? DateOfIncorporation,
//    string? RegisteredAddressLine,
//    string? RegisteredCity,
//    string? RegisteredState,
//    string? RegisteredPincode,
//    string? AuthorizedSignatoryName,
//    string? AuthorizedSignatoryDesignation,
//    string? AuthorizedSignatoryPan,
//    string? AuthorizedSignatoryAadhaarLast4);

public record UpsertCorporateKycRequest(
    // Company Details
    string? EntityName,
    CorporateEntityType? EntityType,
    string? CinOrRegistrationNo,
    string? PanNumber,
    string? Gstin,
    DateOnly? DateOfIncorporation,
    string? PlaceOfIncorporation,
    string? CountryOfIncorporation,

    // Registered Office
    RegisteredOfficeRequest? RegisteredOffice,

    // Contact Details
    List<CorporateContactDetailRequest> ContactDetails,

    // Directors
    List<CorporateDirectorRequest> Directors,

    // Beneficial Owners
    List<CorporateBeneficialOwnerRequest> BeneficialOwners,

    // Authorized Signatories
    List<CorporateAuthorizedSignatoryRequest> AuthorizedSignatories
);
public record RegisteredOfficeRequest(
    string? AddressLine1,
    string? AddressLine2,
    string? AddressLine3,
    string? City,
    string? State,
    string? Pincode,
    string? Country
);
public record CorporateContactDetailRequest(
    string? PhoneNumber,
    string? Email,
    string? Website
);
public record CorporateDirectorRequest(
    string? Name,
    string? Designation,
    string? Din,
    string? Pan,
    DateOnly? DateOfBirth
);
public record CorporateBeneficialOwnerRequest(
    string? Name,
    decimal? OwnershipPercentage,
    string? Pan,
    string? Din,
    string? Nationality,
    string? Address
);
public record CorporateAuthorizedSignatoryRequest(
    string? Name,
    string? Designation,
    string? Pan,
    string? Din,
    string? Email,
    string? PhoneNumber,
    string? AadhaarLast4
);


// ============================================================
// KYC documents
// ============================================================
public record UploadKycDocumentRequest(KycDocType DocType, string? DocNumber);
public record KycDocumentDto(
    long DocumentId,
    long MemberId,
    string DocType,
    string? DocNumber,
    string FilePath,
    string FileHash,
    string Status,
    string? RejectionReason,
    DateTime UploadedAt,
    DateTime? VerifiedAt,
    long? VerifiedBy);

public record VerifyKycDocumentRequest(
    bool Verified,             // true=VERIFIED, false=REJECTED
    string? RejectionReason);

// ============================================================
// KYC review (status transitions)
// ============================================================
public record KycReviewRequest(
    KycStatus ToStatus,
    string? Remarks);

public record KycReviewDto(
    long ReviewId,
    long MemberId,
    string FromStatus,
    string ToStatus,
    string? Remarks,
    long ReviewedBy,
    DateTime ReviewedAt);

// ============================================================
// Tier promotion (MINIMAL -> FULL)
// ============================================================
public record PromoteToFullKycRequest(); // no body; presence of detail row + docs is the condition  it is correct 


