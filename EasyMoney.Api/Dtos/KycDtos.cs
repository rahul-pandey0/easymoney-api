using EasyMoney.Api.Domain;

namespace EasyMoney.Api.Dtos;

// ============================================================
// Member CRUD
// ============================================================
public record CreateMemberRequest(
    MemberType MemberType,
    string FullName,
    string Phone,
    string? Email,
    string? AddressLine,
    string? City,
    string? State,
    string? Pincode,
    string? BankAccountNo,
    string? BankIfsc,
    string? BankHolderName);

public record UpdateMemberRequest(
    string? FullName,
    string? Phone,
    string? Email,
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
    DateTime CreatedAt);

// ============================================================
// Individual / Corporate full KYC detail
// ============================================================
public record UpsertIndividualKycRequest(
    DateOnly? DateOfBirth,
    Gender? Gender,
    string? FatherOrSpouseName,
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
    string? PermanentPincode);

public record UpsertCorporateKycRequest(
    CorporateEntityType? EntityType,
    string? CinOrRegistrationNo,
    string? PanNumber,
    string? Gstin,
    DateOnly? DateOfIncorporation,
    string? RegisteredAddressLine,
    string? RegisteredCity,
    string? RegisteredState,
    string? RegisteredPincode,
    string? AuthorizedSignatoryName,
    string? AuthorizedSignatoryDesignation,
    string? AuthorizedSignatoryPan,
    string? AuthorizedSignatoryAadhaarLast4);

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
public record PromoteToFullKycRequest(); // no body; presence of detail row + docs is the condition
