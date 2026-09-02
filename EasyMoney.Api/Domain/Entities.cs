using System.Text.Json.Serialization;
namespace EasyMoney.Api.Domain;

public class Tenant
{
    public long TenantId { get; set; }
    public string Name { get; set; } = null!;
    public string? RegistrationNumber { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? OrgEmail { get; set; }
    public string? ContactPersonName { get; set; }
    public string? ContactPersonPhone { get; set; }
    public long? CreatedBy { get; set; }
    public TenantStatus Status { get; set; } = TenantStatus.ACTIVE;
    public DateOnly? StartDate { get; set; }        // Tenant created date
    public DateOnly? EffectiveDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? AuthorizedBy { get; set; }
    public DateTime? AuthorizedAt { get; set; }
    public SchemeConfig SchemeConfig { get; set; } = null!;
    public bool AuthorisationRequired { get; set; }
    public bool SmsNotification { get; set; }
    public bool EmailNotification { get; set; }
    public byte[]? LogoData { get; set; }
    public string? LogoContentType { get; set; }
    public string? LogoFileName { get; set; }

}
public class GeneralLedgerMaster
{
    public int GlId { get; set; } 
    public string? Code { get; set; } = null!;
    public string? Name { get; set; } = null!;  
    public string? Description { get; set; }
    public bool? Forbank { get; set; }
    public string Category { get; set; }
    public bool? IsReported { get; set; }
    public bool? HasTransactions { get; set; }
    public bool? HasGst { get; set; }
    public DateTime? CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public DateTime? AuthorizedAt { get; set; }
    public long? AuthorizedBy { get; set; }
    public string? status { get; set; }
    public int? ParentGl { get; set; }
    public string? Type { get; set; }
} 

public class SchemeConfig
{
    public long TenantId { get; set; }
    public int TenureMonths { get; set; } = 20;
    public decimal OrgFeePct { get; set; } = 5.00m;
    public decimal SifinCommissionPct { get; set; } = 1.00m;
    public decimal MinBidPct { get; set; } = 15.00m;
    public decimal MaxBidPct { get; set; } = 50.00m;
    public decimal EarlyExitPenaltyPct { get; set; } = 15.00m;
    public int MinInstallmentsForEligibility { get; set; } = 2;
    public int BiddingWindowOpenDay { get; set; } = 1;
    public int BiddingDayOfMonth { get; set; } = 15;
    public decimal NoBidDefaultDividendPct { get; set; } = 0.00m;
    public KycMode KycMode { get; set; } = KycMode.MINIMAL_FIRST;
    // Per-tenant maker-checker flag (default ON). When OFF, actions execute
    // immediately and an approval_request row is still written with status=AUTO_APPROVED.
    public bool MakerCheckerEnabled { get; set; } = true;
    //public long? UpdatedBy { get; set; }
    //public DateTime? UpdatedAt { get; set; }
    //public long? AuthorizedBy { get; set; }
    //public DateTime? AuthorizedAt { get; set; }
    //public Tenant Tenant { get; set; } = null!;
    // -----------------------------
    // Bank Information
    // -----------------------------
    public string? BankName { get; set; }
    public string? CustAddress1 { get; set; }
    public string? CustAddress2 { get; set; }
    public string? CustAddress3 { get; set; }
    public string? Email { get; set; }
    public string? PhNum { get; set; }
    //public string? Fax { get; set; }
    public string? RdStatus { get; set; }

    // -----------------------------
    // Bonus / Commission
    // -----------------------------
    public decimal GrossBonus { get; set; }
    public decimal TenantCommission { get; set; }
    public decimal NetBonus { get; set; }

    // -----------------------------
    // GL Accounts / Reserve
    // -----------------------------
    public string? Reserve1 { get; set; }
    public string? Reserve2 { get; set; }
    public string? PoolMoney { get; set; }
    public string? TenantPin { get; set; }
    public string? LoanAssetGL { get; set; }
    public string? SifinPayable { get; set; }

    // -----------------------------
    // Time Change
    // -----------------------------
    public decimal TimeChPass { get; set; }

    // -----------------------------
    // Penalty Accounts
    // -----------------------------
    public string? PenaltyAcc { get; set; }
    public string? NMPenaltyAcc { get; set; }

    // -----------------------------
    // Interest Configuration
    // -----------------------------
    public decimal MinimumRate { get; set; }
    public decimal MaximumRate { get; set; }
    public int MinimumPeriod { get; set; }
    public int MaximumPeriod { get; set; }

    // -----------------------------
    // Tax Accounts
    // -----------------------------
    public string? TdsAc { get; set; }
    public string? ServicesTax { get; set; }

    // -----------------------------
    // Audit Fields
    // -----------------------------
    public long? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? AuthorizedBy { get; set; }
    public DateTime? AuthorizedAt { get; set; }

    // Navigation Property
    public Tenant Tenant { get; set; } = null!;
    public string? SchemeName { get; set; }

    public decimal FixedRate { get; set; }

    public string? GstGl { get; set; }
    public string? SchemeCode { get; set; }
    public int? SchemeId  { get; set; }
        public int? OrgFeeGlId { get; set; }  // GL for Organization Fee
    public int? SifinCommissionGlId { get; set; }  // GL for Sifin Commission

    public decimal? MinimumInstallmentAmount { get; set; }
    public decimal? MaximumInstallmentAmount { get; set; }

}



public class SchemeMaster 
{
    public long SchemeId { get; set; } 
    public int TenureMonths { get; set; } = 20;
    public decimal OrgFeePct { get; set; } = 5.00m;
    public decimal SifinCommissionPct { get; set; } = 1.00m;
    public decimal MinBidPct { get; set; } = 15.00m;
    public decimal MaxBidPct { get; set; } = 50.00m;
    public decimal EarlyExitPenaltyPct { get; set; } = 15.00m;
    public int MinInstallmentsForEligibility { get; set; } = 2;
    public int BiddingWindowOpenDay { get; set; } = 1;
    public int BiddingDayOfMonth { get; set; } = 15;
    public decimal NoBidDefaultDividendPct { get; set; } = 0.00m;
    public KycMode KycMode { get; set; } = KycMode.MINIMAL_FIRST;
    public bool MakerCheckerEnabled { get; set; } = true;
    public string? BankName { get; set; }
    public string? CustAddress1 { get; set; }
    public string? CustAddress2 { get; set; }
    public string? CustAddress3 { get; set; }
    public string? Email { get; set; }
    public string? PhNum { get; set; }
  
    public string? RdStatus { get; set; }
    public decimal GrossBonus { get; set; }
    public decimal TenantCommission { get; set; }
    public decimal NetBonus { get; set; }
    public string? Reserve1 { get; set; }
    public string? Reserve2 { get; set; }
    public string? PoolMoney { get; set; }
    public string? TenantPin { get; set; }
    public string? LoanAssetGL { get; set; }
    public string? SifinPayable { get; set; }
    public decimal TimeChPass { get; set; }
    public string? PenaltyAcc { get; set; }
    public string? NMPenaltyAcc { get; set; }
    public decimal MinimumRate { get; set; }
    public decimal MaximumRate { get; set; }
    public int MinimumPeriod { get; set; }
    public int MaximumPeriod { get; set; }
    public string? TdsAc { get; set; }
    public string? ServicesTax { get; set; }
    public long? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? AuthorizedBy { get; set; }
    public DateTime? AuthorizedAt { get; set; }
    //public string Tenant { get; set; } = null!;
    public string? SchemeName { get; set; }
    public decimal FixedRate { get; set; }
    public string? GstGl { get; set; } 
    public string? SchemeCode { get; set; }
    public decimal? MinimumInstallmentAmount { get; set; }
    public decimal? MaximumInstallmentAmount { get; set; }

}

public class AppUser
{
    public long UserId { get; set; }
    public string? UserName { get; set; }
 
    public Branch? Branch { get; set; }
    public long? TenantId { get; set; }
    public long? MemberId { get; set; }
    public long? BranchId { get; set; }
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public UserRole Role { get; set; }
    public long? CreatedBy { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? AuthorizedBy { get; set; }
    public DateTime? AuthorizedAt { get; set; }
    public Member? Member { get; set; }
    public Tenant? Tenant { get; set; }
}

public class RefreshToken
{
    public long TokenId { get; set; }
    public long UserId { get; set; }
    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Member
{
    public long MemberId { get; set; }
    public long TenantId { get; set; } 
    public long? BranchId { get; set; }

    public string CustomerIdentifierCode { get; set; } = string.Empty;
    public MemberType MemberType { get; set; }
    public string FullName { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string? Email { get; set; }
    public string? PanNumber { get; set; }
    public string? IdType { get; set; }
    public string? IdNumber { get; set; }
    public string? AddressLine { get; set; }
    public string? AddressProof { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }
    public KycTier KycTier { get; set; } = KycTier.MINIMAL;
    public KycStatus KycStatus { get; set; } = KycStatus.PENDING;
    public DateTime? KycApprovedAt { get; set; }
    public long? KycApprovedBy { get; set; }
    public string? BankAccountNo { get; set; }
    public string? BankIfsc { get; set; }
    public string? BankHolderName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}


public class RelationMaster
{
    public long RelationId { get; set; }  
    public long TenantId { get; set; }
    public long? BranchId { get; set; }
    public long? MemberId { get; set; }
    public long? MemberNo { get; set; }
    public string? Name { get; set; }
    public string? PhoneNo { get; set; } 
    public string? AccountNo { get; set; }
    public string? Remarks { get; set; } 
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public long? CreatedBy { get; set; }
    public long? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public long? UpdateBy { get; set; }  // ✅ This should be long? to match database
    public DateTime? UpdateAt { get; set; } 
    public string? AuthStatus { get; set; } 

}

//public class IndividualKycDetail
//{
//    public long MemberId { get; set; }
//    public DateOnly? DateOfBirth { get; set; }
//    public Gender? Gender { get; set; }
//    public string? FatherOrSpouseName { get; set; }
//    public string? PanNumber { get; set; }
//    public string? AadhaarNumber { get; set; }
//    public string? AadhaarLast4 { get; set; }
//    public string? Occupation { get; set; }
//    public IncomeBand? AnnualIncomeBand { get; set; }
//    public string? NomineeName { get; set; }
//    public string? NomineeRelation { get; set; }
//    public DateOnly? NomineeDob { get; set; }
//    public string? PermanentAddressLine { get; set; }
//    public string? PermanentCity { get; set; }
//    public string? PermanentState { get; set; }
//    public string? PermanentPincode { get; set; }
//    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
//}

public class IndividualKycDetail
{
    public long? BranchId { get; set; }

    public long MemberId { get; set; }
    public string? CustomerId { get; set; }
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? MobileNumber { get; set; }
    public string? ResidencePhone { get; set; }
    public string? OfficePhone { get; set; }
    public string? Email { get; set; }
    public int? Age { get; set; }
    public string? Education { get; set; }
    public string? MaritalStatus { get; set; }

    public DateOnly? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public string? FatherOrSpouseName { get; set; }
    public string? PanNumber { get; set; }
    public string? AadhaarNumber { get; set; }
    public string? AadhaarLast4 { get; set; }
    public string? Occupation { get; set; }
    public IncomeBand? AnnualIncomeBand { get; set; }
    public string? NomineeName { get; set; }
    public string? NomineeRelation { get; set; }
    public DateOnly? NomineeDob { get; set; }
    public string? PermanentAddressLine { get; set; }
    public string? PermanentCity { get; set; }
    public string? PermanentState { get; set; }
    public string? PermanentPincode { get; set; }

    public string? IdType { get; set; }
    public string? IdNumber { get; set; }
    public string? AddressProof { get; set; }
    public string? DocumentNumber { get; set; }
    public string? CustomerImage { get; set; }
    public string? IdImage { get; set; }
    public string? DocumentImage { get; set; }

    public string? BankName { get; set; }
    public string? SavingsAccountNumber { get; set; }
    public string? CurrentAccountNumber { get; set; }

    public int? TotalFamilyMembers { get; set; }
    public int? DependentFamilyMembers { get; set; }
    public int? EarningFamilyMembers { get; set; }
    public string? AdditionalPersonalDetails { get; set; }
    public string? Remarks { get; set; }
    public string? Remarks1 { get; set; }

    public string? BikeModel { get; set; }
    public string? BikeCompany { get; set; }
    public string? CarModel { get; set; }
    public string? CarCompany { get; set; }
    public string? TractorModel { get; set; }
    public string? TractorCompany { get; set; }
    public string? HeavyVehicleModel { get; set; }
    public string? HeavyVehicleCompany { get; set; }

    public string? AgricultureLand { get; set; }
    public decimal? AgricultureArea { get; set; }
    public string? AgricultureSurveyNo { get; set; }
    public decimal? AgricultureValue { get; set; }

    public string? SiteDetails { get; set; }
    public decimal? SiteArea { get; set; }
    public string? SiteSurveyNo { get; set; }
    public decimal? SiteValue { get; set; }

    public string? MembershipNumber { get; set; }
    public string? AccountType { get; set; }
    public string? AccountNumber { get; set; }

    public string? PlantationDetails { get; set; }
    public decimal? PlantationArea { get; set; }
    public string? PlantationSurveyNo { get; set; }
    public decimal? PlantationValue { get; set; }

    public string? HouseDetails { get; set; }
    public decimal? HouseArea { get; set; }
    public string? HouseNumber { get; set; }
    public decimal? HouseValue { get; set; }

    public string? EmploymentNature { get; set; }
    public string? EmployerName { get; set; }
    public decimal? SalaryDetails { get; set; }
    public string? Designation { get; set; }
    public string? OrganizationNature { get; set; }
    public string? Department { get; set; }
    public string? OfficeAddress { get; set; }

    public DateTime UpdatedAt { get; set; }
}
//public class CorporateKycDetail
//{
//    public long MemberId { get; set; }
//    public CorporateEntityType? EntityType { get; set; }
//    public string? CinOrRegistrationNo { get; set; }
//    public string? PanNumber { get; set; }
//    public string? Gstin { get; set; }
//    public DateOnly? DateOfIncorporation { get; set; }
//    public string? RegisteredAddressLine { get; set; }
//    public string? RegisteredCity { get; set; }
//    public string? RegisteredState { get; set; }
//    public string? RegisteredPincode { get; set; }
//    public string? AuthorizedSignatoryName { get; set; }
//    public string? AuthorizedSignatoryDesignation { get; set; }
//    public string? AuthorizedSignatoryPan { get; set; }
//    public string? AuthorizedSignatoryAadhaarLast4 { get; set; }
//    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
//}
public class CorporateKycDetail
{
    public long MemberId { get; set; }
    public long BranchId { get; set; }

    // Company Details
    public string? EntityName { get; set; }
    public CorporateEntityType? EntityType { get; set; }
    public string? CinOrRegistrationNo { get; set; }
    public string? PanNumber { get; set; }
    public string? Gstin { get; set; }
    public DateOnly? DateOfIncorporation { get; set; }
    public string? PlaceOfIncorporation { get; set; }
    public string? CountryOfIncorporation { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public RegisteredOffice? RegisteredOffice { get; set; }

    public ICollection<CorporateContactDetail> ContactDetails { get; set; } = new List<CorporateContactDetail>();

    public ICollection<CorporateDirector> Directors { get; set; } = new List<CorporateDirector>();

    public ICollection<CorporateBeneficialOwner> BeneficialOwners { get; set; } = new List<CorporateBeneficialOwner>();

    public ICollection<CorporateAuthorizedSignatory> AuthorizedSignatories { get; set; } = new List<CorporateAuthorizedSignatory>();
}

public class RegisteredOffice
{
    public long Id { get; set; }

    public long MemberId { get; set; }

    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? AddressLine3 { get; set; }

    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }
    public string? Country { get; set; }
    [JsonIgnore]
    public CorporateKycDetail? CorporateKycDetail { get; set; }
}
public class CorporateContactDetail
{
    public long Id { get; set; }

    public long MemberId { get; set; }

    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    [JsonIgnore]
    public CorporateKycDetail? CorporateKycDetail { get; set; }
}
public class CorporateDirector
{
    public long Id { get; set; }

    public long MemberId { get; set; }

    public string? Name { get; set; }
    public string? Designation { get; set; }
    public string? Din { get; set; }
    public string? Pan { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    [JsonIgnore]
    public CorporateKycDetail? CorporateKycDetail { get; set; }
}
public class CorporateBeneficialOwner
{
    public long Id { get; set; }

    public long MemberId { get; set; }

    public string? Name { get; set; }
    public decimal? OwnershipPercentage { get; set; }
    public string? Pan { get; set; }
    public string? Din { get; set; }
    public string? Nationality { get; set; }
    public string? Address { get; set; }
    [JsonIgnore]
    public CorporateKycDetail? CorporateKycDetail { get; set; }
}
public class CorporateAuthorizedSignatory
{
    public long Id { get; set; }

    public long MemberId { get; set; }

    public string? Name { get; set; }
    public string? Designation { get; set; }
    public string? Pan { get; set; }
    public string? Din { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AadhaarLast4 { get; set; }
    [JsonIgnore]
    public CorporateKycDetail? CorporateKycDetail { get; set; }
}



public class KycDocument
{
    public long DocumentId { get; set; }
    public long TenantId { get; set; }
    public long MemberId { get; set; }
    public KycDocType DocType { get; set; }
    public string? DocNumber { get; set; }
    public string FilePath { get; set; } = null!;
    public string FileHash { get; set; } = null!;
    public KycDocStatus Status { get; set; } = KycDocStatus.UPLOADED;
    public string? RejectionReason { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedAt { get; set; }
    public long? VerifiedBy { get; set; }
}

public class KycReview
{
    public long ReviewId { get; set; }
    public long TenantId { get; set; }
    public long MemberId { get; set; }
    public KycStatus FromStatus { get; set; }
    public KycStatus ToStatus { get; set; }
    public string? Remarks { get; set; }
    public long ReviewedBy { get; set; }
    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;
}

public class Account
{
    public long AccountId { get; set; }
    public long TenantId { get; set; }
    public long? BranchId { get; set; }

    public long MemberId { get; set; }
    public string AccountNumber { get; set; } = null!;
    public decimal MonthlyContribution { get; set; }
    public DateOnly AccountOpenDate { get; set; }
    public DateOnly TenureEndDate { get; set; }
    public AccountStatus Status { get; set; } = AccountStatus.ACTIVE;
    public int InstallmentsPaid { get; set; }
    public bool IsPrized { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; } 

    public long? AuthorizedBy { get; set; }
    public DateTime? AuthorizedAt { get; set; }
    //new columns
    public string? OldAccountNo { get; set; }
    public string? PhoneNo { get; set; } 
    public string? CustomerName { get; set; }
    public decimal InterestRate { get; set; }
    public decimal TargetAmount { get; set; }
    public DateOnly? PaymentDate { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal? LoanAmount { get; set; }
    public decimal BonusAmount { get; set; }
    public decimal InterestAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Remarks { get; set; }
    public int?  SchemeId { get; set; }
    public string? FirstPaymentFlag { get; set; }
    public string? IsBidding { get; set; }
    public string? CustomerCode { get; set; } 


}

public class BiddingCycle
{
    public long CycleId { get; set; }
    public long TenantId { get; set; }
    public DateOnly CycleMonth { get; set; }
    public DateTime WindowOpenAt { get; set; }
    public DateTime WindowCloseAt { get; set; }
    public decimal? GrossCorpus { get; set; }
    public decimal? OrgFeeAmount { get; set; }
    public decimal? BidPool { get; set; }
    public long? WinnerAccountId { get; set; }
    public decimal? WinnerBidPct { get; set; }
    public decimal? LoanDisbursed { get; set; }
    public decimal? DividendPool { get; set; }
    public CycleStatus Status { get; set; } = CycleStatus.OPEN;
    public DateTime? ResolvedAt { get; set; }
    public long BranchId { get; set; }
    public decimal OrgFeePct { get; set; } = 5.00m;
    public decimal SifinCommissionPct { get; set; }
    public decimal TenantCommissionPct { get; set; } 

    //public long? BidRefNo { get; set; }

}

public class Bid
{
    public long BidId { get; set; }
    public long CycleId { get; set; }
    public long AccountId { get; set; }
    public decimal BidPct { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsWinner { get; set; }
    public long? TenantId { get; set; }
    public long? BranchId { get; set; }
    public bool IsApproved { get; set; }
    public long? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public decimal OrgFeePct { get; set; } = 5.00m;
    public decimal SifinCommissionPct { get; set; }
    public decimal FixedRate { get; set; }
    public decimal TotalBid { get; set; }
    public decimal TragetAmount { get; set; }
    public decimal AllotmentAmount { get; set; }
    public decimal TenantCommissionPct { get; set; }
    //public long? BidRefNo { get; set; }

}

public class Loan
{
    public long LoanId { get; set; }
    public long TenantId { get; set; }
    public long AccountId { get; set; }
    public long CycleId { get; set; }
    public decimal PrincipalAmount { get; set; }
    public DateTime DisbursedAt { get; set; }
    public decimal OutstandingBalance { get; set; }
    public string? Status { get; set; } 
    public DateTime? RepaidAt { get; set; }
    public long? AuthorizedBy { get; set; }
    public DateTime? AuthorizedAt { get; set; }

    public string? PhoneNumber { get; set; }
    public string? CustomerName { get; set; }
    public DateOnly? BidDate { get; set; }   
    //public string? CoopName { get; set; }
    //public string? CoopMobileNumber { get; set; } 
    //public long? CoopAccountId { get; set; } 
    public long? BranchId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? CreatedBy { get; set; }
    public bool AuthStatus { get; set; }
    public decimal? OrgFeeAmount { get; set; }
    public decimal? SifinCommission { get; set; }
    public decimal? NetDisbursementAmount { get; set; }
    public decimal? ProcessingFee { get; set; }
    public List<CoBorrower>? CoBorrowerDetails { get; set; }
    public string? LoanRemark { get; set; }
    public string? LoanApplicationStatus { get; set; }
    public string? SecurityDocStatus { get; set; }
    public string? SecurityDocRemarks { get; set; }

    // Cheque Details
    public string? ChequeObtained { get; set; }
    public string? ChequeAccountNo { get; set; }
    public string? ChequeBankName { get; set; }
    public string? ChequeNo { get; set; } 
    public string? LoanReleaseStatus { get; set; }
    public DateTime? ChequeDate { get; set; }

    public string ? Remarks { get; set; }

    public string? DisbursementStatus { get; set; } 

}

public class CoBorrower
{
    public long TenantId { get; set; }
    public long CoBorrowerId { get; set; }
    public long LoanId { get; set; } 
    public string? CoBorrowerName { get; set; }
    public string? CoBorrowerPhone { get; set; }
    public string? CoBorrowerEmail { get; set; }
    public string? CoBorrowerAddress { get; set; }
    public long? CoBorrowerAccountId { get; set; }
    public string? CoBorrowerAccountNumber { get; set; }
     public string? CoopName { get; set; }
    public string? CoopMobileNumber { get; set; }
    public long? CoopAccountId { get; set; }
    public string? CoopAccountNumber { get; set; }
    public string? CoBorrowerRemarks { get; set; }
    public bool IsPrimaryCoBorrower { get; set; } 
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public bool IsActive { get; set; } = true;

    //public virtual Loan ParentLoan { get; set; }
}

public class LedgerEntry
{
    public long EntryId { get; set; }
    public long TenantId { get; set; }
    public long AccountId { get; set; }
    public long? CycleId { get; set; }
    public long? LinkedEntryId { get; set; }  // PAYMENT_RECEIVED rows: points to the CONTRIBUTION_DUE being settled
    public LedgerEntryType EntryType { get; set; }
    public decimal Amount { get; set; }
    public DateOnly EntryDate { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? CreatedBy { get; set; }
    public string? Remarks { get; set; }
    public string? VoucherNo { get; set; }
    public long? GlAccountId { get; set; }
    public string? GlAccountName { get; set; }
    public string? PhoneNumber { get; set; }


    public long? PaymentDetailId { get; set; } // Link to payment detail
    public long? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool? PaymentStatus { get; set; }
    public DateTime? PaymentDate { get; set; }
}

public class Dividend
{
    public long DividendId { get; set; }
    public long CycleId { get; set; }
    public long AccountId { get; set; }
    public decimal Amount { get; set; }
}

public class GlAccount
{
    public long GlAccountId { get; set; }
    public long TenantId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public GlAccountClass AccountClass { get; set; }
    public string? ParentCode { get; set; }
    public bool IsActive { get; set; } = true;
    public long? CreatedBy { get; set; }
    public long? AuthorizedBy { get; set; }
    public DateTime? AuthorizedAt { get; set; }
    public bool Forbank { get; set; }
    public bool IsReported { get; set; }
    public bool HasTransactions { get; set; }
    public bool HasGst { get; set; }
}

public class JournalEntry
{
    public long JournalId { get; set; }
    public long TenantId { get; set; }
    public DateOnly EntryDate { get; set; }
    public JournalSourceType SourceType { get; set; }
    public long? SourceId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? CreatedBy { get; set; }
    public long? AuthorizedBy { get; set; }
    public DateTime? AuthorizedAt { get; set; }
    public ICollection<JournalLine> Lines { get; set; } = new List<JournalLine>();
    public long? BranchId { get; set; }
    public long? GlId { get; set; } 

}

public class JournalLine
{
    public long LineId { get; set; }
    public long JournalId { get; set; }
    public EntryTarget EntryTarget { get; set; }
    public long? GlAccountId { get; set; }
    public long? MemberAccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal? RunningBalance { get; set; }
    public JournalEntry Journal { get; set; } = null!;
    public GlAccount? GlAccount { get; set; }
    public Account? MemberAccount { get; set; }
}

public class GlAccountBalance
{
    public long?Tenantid { get; set; } 
    public long? BranchId { get; set; }
    public long GlAccountId { get; set; }
    public decimal Balance { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class MemberAccountBalance
{
    public long? Tenantid { get; set; }
    public long? BranchId { get; set; }
    public long AccountId { get; set; }
    public decimal Balance { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public class GLAccountDailyBalance
{
    public long Id { get; set; }
    public long GlId { get; set; }
    public long TenantId { get; set; }  
    public long? BranchId { get; set; } 
    //public DateTime TransactionDate { get; set; }
    public decimal Balance { get; set; }
    //public decimal DebitAmount { get; set; }
    //public decimal CreditAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    //public virtual GeneralLedgerMaster GLAccount { get; set; }
}
public class IdempotencyLog
{
    public string IdempotencyKey { get; set; } = null!;
    public long TenantId { get; set; }
    public string Endpoint { get; set; } = null!;
    public string? ResponseBody { get; set; }
    public int? StatusCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AuditLog
{
    public long AuditId { get; set; }
    public long? TenantId { get; set; }
    public long? UserId { get; set; }
    public string Action { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public long EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Notification
{
    public long NotificationId { get; set; }
    public long TenantId { get; set; }
    public long? UserId { get; set; }       // null = broadcast to all org users of the tenant
    public long? AccountId { get; set; }
    public string Type { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ApprovalRequest
{
    public long RequestId { get; set; }
    public long? TenantId { get; set; }
    public ApprovalActionType ActionType { get; set; }
    public string EntityType { get; set; } = null!;
    public long? EntityId { get; set; }
    public string Payload { get; set; } = null!; // JSON
    public ApprovalStatus Status { get; set; } = ApprovalStatus.PENDING;
    public long RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public long? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionRemarks { get; set; }
}
//public class UserBranch
//{
//    public long UserBranchId { get; set; }

//    public long UserId { get; set; }

//    public long BranchId { get; set; }

//    public bool IsActive { get; set; } = true;

//    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
//}

public class IndividualDetail
{
    public long IndividualDetailId { get; set; }

    // Personal Details
    public string? CustomerId { get; set; }
    public string? Name { get; set; }
    public string? FatherOrSpouseName { get; set; }
    public string? Address { get; set; }
    public string? MobileNumber { get; set; }
    public string? ResidencePhone { get; set; }
    public string? OfficePhone { get; set; }
    public string? Email { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public int? Age { get; set; }
    public string? Education { get; set; }
    public string? MaritalStatus { get; set; }

    // ID Details
    public string? IdType { get; set; }
    public string? IdNumber { get; set; }
    public string? AddressProof { get; set; }
    public string? DocumentNumber { get; set; }
    public string? CustomerImage { get; set; }
    public string? IdImage { get; set; }
    public string? DocumentImage { get; set; }

    // Member Details
    public string? MembershipNumber { get; set; }
    public string? AccountType { get; set; }
    public string? AccountNumber { get; set; }

    // Other Bank Details
    public string? BankName { get; set; }
    public string? SavingsAccountNumber { get; set; }
    public string? CurrentAccountNumber { get; set; }

    // Existing KYC Fields
    public string? NomineeName { get; set; }
    public string? NomineeRelation { get; set; }
    public DateOnly? NomineeDob { get; set; }
    public string? Occupation { get; set; }
    public string? AnnualIncomeBand { get; set; }
    public string? PermanentAddressLine { get; set; }
    public string? PermanentCity { get; set; }
    public string? PermanentState { get; set; }
    public string? PermanentPincode { get; set; }
    public string? Gender { get; set; }
    public string? PanNumber { get; set; }
    public string? AadhaarNumber { get; set; }

    // Family Details
    public int? TotalFamilyMembers { get; set; }
    public int? DependentFamilyMembers { get; set; }
    public int? EarningFamilyMembers { get; set; }
    public string? AdditionalPersonalDetails { get; set; }
    public string? Remarks { get; set; }
    public string? Remarks1 { get; set; }

    // Vehicle Details
    public string? BikeModel { get; set; }
    public string? BikeCompany { get; set; }
    public string? CarModel { get; set; }
    public string? CarCompany { get; set; }
    public string? TractorModel { get; set; }
    public string? TractorCompany { get; set; }
    public string? HeavyVehicleModel { get; set; }
    public string? HeavyVehicleCompany { get; set; }

    // Property Details
    public string? AgricultureLand { get; set; }
    public decimal? AgricultureArea { get; set; }
    public string? AgricultureSurveyNo { get; set; }
    public decimal? AgricultureValue { get; set; }

    public string? SiteDetails { get; set; }
    public decimal? SiteArea { get; set; }
    public string? SiteSurveyNo { get; set; }
    public decimal? SiteValue { get; set; }

    public string? PlantationDetails { get; set; }
    public decimal? PlantationArea { get; set; }
    public string? PlantationSurveyNo { get; set; }
    public decimal? PlantationValue { get; set; }

    public string? HouseDetails { get; set; }
    public decimal? HouseArea { get; set; }
    public string? HouseNumber { get; set; }
    public decimal? HouseValue { get; set; }

    // Occupational Details
    public string? EmploymentNature { get; set; }
    public string? EmployerName { get; set; }
    public decimal? SalaryDetails { get; set; }
    public string? Designation { get; set; }
    public string? OrganizationNature { get; set; }
    public string? Department { get; set; }
    public string? OfficeAddress { get; set; }
}
public class Branch
{
    public long BranchId { get; set; }

    public string BranchCode { get; set; } = default!;

    public string BranchName { get; set; } = default!;

    public long TenantId { get; set; }

    public long? BankId { get; set; }

    public string? RegistrationNo { get; set; }

    public DateOnly RegistrationDate { get; set; }

    public DateOnly BranchRegistrationDate { get; set; }

    public string? Address { get; set; }

    public string? PhoneNumber { get; set; }

    //public string? Fax { get; set; }

    public string? Email { get; set; }

    public string? ReferenceNo { get; set; }

    public long? CashGlId { get; set; }

    public long? TransferGlId { get; set; }

    public DateOnly CutoffDate { get; set; }

    public DateOnly BiddingDate { get; set; }

    public DateOnly? BonusPaymentDate { get; set; }

    //public BranchStatus Status { get; set; } 
    public BranchStatus Status { get; set; } = BranchStatus.ACTIVE; // Default to Active}

    public decimal MinimumRate { get; set; }

    public decimal MaximumRate { get; set; }

    public decimal Penalty { get; set; }

    public bool DoublePaymentAllowed { get; set; }

    public decimal MinimumInstallmentAmount { get; set; }

    public decimal MaximumInstallmentAmount { get; set; }

    public decimal MinimumIncrementAmount { get; set; }

    public string? OtherBank1 { get; set; }

    public string? OtherBank2 { get; set; }

    public long? AdjustmentGlId { get; set; }

    public DateTime CreatedAt { get; set; }

    public long CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }
}


public class PaymentReportDto
{
    public long JournalId { get; set; }
    public long TenantId { get; set; }
    public decimal? Amount { get; set; } 

    public DateOnly EntryDate { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public long? BranchId { get; set; }
    public string CustomerName { get; set; } 
    public string? AccountNo { get; set; }

}
public class ReportFilterPayload
{
    public long? TenantId { get; set; }
    public long? BranchId { get; set; }
    public long? GlId { get; set; } 

    public string? Type { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public long JournalId { get; set; }
    public decimal? Amount { get; set; }

    public DateOnly EntryDate { get; set; }
    public string? SourceType { get; set; } 
    public string? PaymentMethod { get; set; } 
    public string? Description { get; set; }
    public decimal? Debit { get; set; }
    public decimal? Credit { get; set; }
    public string? CustomerName { get; set; }
    public string? AccountNo { get; set; }
    public string? Status { get; set; } 

    public ReportsType ReportType { get; set; }
    //public string Filters { get; internal set; }
}
public class ReportResponse
{
    public bool Success { get; set; }
    public ReportsType ReportType { get; set; }
    public object Data { get; set; }
    public string Message { get; set; }

}
public class AccountOpenReportDto
{
    public long AccountId { get; set; }
    public long TenantId { get; set; }
    public long? BranchId { get; set; }
    public string AccountNumber { get; set; }
    public DateOnly AccountOpenDate { get; set; }
    public string Status { get; set; }
    public string AccountType { get; set; }  // ✅ String representation of enum
    public string FullName { get; set; }
    public string Phone { get; set; }
    public string? Email { get; set; }
    public decimal? Balance { get; set; }
    public long MemberId { get; set; }
    public decimal MonthlyContribution { get; set; }
    public DateOnly TenureEndDate { get; set; }

}
public class KycReportDto
{
    public long MemberId { get; set; }
    public long TenantId { get; set; }
    public long? BranchId { get; set; }
    public string MemberType { get; set; }
    public string KycStatus { get; set; }
    public string KycTier { get; set; }
    public DateTime? KycApprovedAt { get; set; }
    public string CustomerIdentifierCode { get; set; } 
    public string FullName { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string? Email { get; set; }
    public string? PanNumber { get; set; }
    public string? IdType { get; set; }
    public string? IdNumber { get; set; }
}


// File: Dtos/BiddingSummaryDtos.cs

public class BiddingSummaryDto
{
    // Cycle Information
    public bool HasActiveBidding { get; set; }
    public long? CycleId { get; set; }
    public DateOnly? CycleMonth { get; set; }
    public string CycleStatus { get; set; }
    public string StatusColor { get; set; }

    // Timeline
    public DateTime? WindowOpenAt { get; set; }
    public DateTime? WindowCloseAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    // Bidding Statistics
    public int TotalBids { get; set; }
    public int ApprovedBids { get; set; }
    public int PendingBids { get; set; }
    public int RejectedBids { get; set; }
    public int UniqueBidders { get; set; }

    // Financial Summary
    public decimal GrossCorpus { get; set; }
    public decimal OrgFeeAmount { get; set; }
    public decimal BidPool { get; set; }
    public decimal LoanDisbursed { get; set; }
    public decimal DividendPool { get; set; }

    // Winner Details (if resolved)
    public WinnerSummaryDto Winner { get; set; }
    public long? WinnerAccountId { get; set; }
    public decimal? WinnerBidPct { get; set; }

    // Bidders List
    public List<BidderSummaryDto> Bidders { get; set; }

    // Messages
    public string Message { get; set; }
    public string NextStep { get; set; }
    public bool CanProceed { get; set; }
}

public class WinnerSummaryDto
{
    public long BidId { get; set; }
    public long AccountId { get; set; }
    public string AccountNumber { get; set; }
    public long MemberId { get; set; }
    public string MemberName { get; set; }
    public string MemberPhone { get; set; }
    public string Email { get; set; }
    public decimal BidPct { get; set; }
    public decimal ForfeitureAmount { get; set; }
    public decimal PrizeAmount { get; set; }
    public decimal NetReceivable { get; set; }
    public DateTime SubmittedAt { get; set; }
}

public class BidderSummaryDto
{
    //public int Rank { get; set; }
    public long BidId { get; set; }
    public long AccountId { get; set; }
    public string AccountNumber { get; set; }
    public long MemberId { get; set; }
    public string MemberName { get; set; }
    public string MemberPhone { get; set; }
    public decimal BidPct { get; set; }
    public DateTime SubmittedAt { get; set; }
    public bool IsApproved { get; set; }
    public bool IsWinner { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public long? ApprovedBy { get; set; }
    public decimal MonthlyContribution { get; set; }
    public int InstallmentsPaid { get; set; }
    //public int TotalInstallments { get; set; }
    public decimal ForfeitureAmount { get; set; }
    public decimal PrizeIfWins { get; set; }
    public string Status { get; set; }
    public string StatusBadge { get; set; }
    public decimal FixedRate { get; set; }
    public decimal TotalBid { get; set; } 
    public decimal TargetAmount { get; set; }
    public decimal AllotmentAmount { get; set; }
}

// Dtos/DisbursementRequestDto.cs
public class DisbursementRequestDto
{
    public long LoanId { get; set; }
    public decimal? Amount { get; set; }
    public string? PaymentMethod { get; set; }
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? IfscCode { get; set; }
    public string? ChequeNumber { get; set; }
    public string? TransactionReference { get; set; }
    public DateTime? DisbursedAt { get; set; }
    public string? Remarks { get; set; }

    // Fee breakdown
    public decimal? ProcessingFee { get; set; }
    public decimal? OrgFeeAmount { get; set; }
    public decimal? SifinCommission { get; set; }
    public decimal? FixedRateAmount { get; set; }
    public decimal? TenantCommission { get; set; }
    public decimal? TdsAmount { get; set; }
    public decimal? OtherDeductions { get; set; }
    public decimal? TenantCommissionPct { get; set; }
    public decimal? BonusPct { get; set; }
    public decimal? SifinCommissionPct { get; set; }
    public decimal? ProcessingFeePct { get; set; }
    public decimal? TdsPct { get; set; }


}

// Dtos/DisbursementVoucherDto.cs
public class DisbursementVoucherDto
{
    public long VoucherId { get; set; }
    public long LoanId { get; set; }
    public string VoucherNumber { get; set; }
    public DateTime VoucherDate { get; set; }
    public string TransactionType { get; set; } // "LOAN_DISBURSEMENT"
    public string PaymentMethod { get; set; }
    public string TransactionReference { get; set; }

    // Amount Details
    public decimal GrossAmount { get; set; }           // Principal Amount
    public decimal FixedRateAmount { get; set; }       // Fixed Rate deduction
    public decimal TenantCommission { get; set; }      // Org Fee
    public decimal SifinCommission { get; set; }       // SIFIN Commission
    public decimal ProcessingFee { get; set; }         // Processing Fee
    public decimal TdsAmount { get; set; }             // TDS Deduction
    public decimal OtherDeductions { get; set; }       // Other deductions
    public decimal NetAmount { get; set; }             // Final disbursed amount

    // Accounting Entries
    public List<VoucherLineDto> DebitEntries { get; set; }
    public List<VoucherLineDto> CreditEntries { get; set; }

    // Additional Info
    public string AccountNumber { get; set; }
    public string AccountHolder { get; set; }
    public string BankName { get; set; }
    public string IfscCode { get; set; }
    public string Remarks { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; }
    public string AuthorizedBy { get; set; }
    public decimal TenantCommissionPct { get; set; }
    public decimal BonusAmount { get; set; }
    public decimal BonusPct { get; set; }
    public decimal SifinCommissionPct { get; set; }
    public decimal ProcessingFeePct { get; set; }
    public decimal TdsPct { get; set; }
    public decimal TotalDeductions { get; set; }

}

public class VoucherLineDto
{
    public string AccountCode { get; set; }
    public string AccountName { get; set; }
    public string AccountType { get; set; } // "Asset", "Liability", "Income", "Expense"
    public decimal Amount { get; set; }
    public string Narration { get; set; }
    public string EntryType {  get; set; }
}


public class PaymentDetail
{
    public long PaymentDetailId { get; set; }
    public long TenantId { get; set; }
    public long AccountId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly PaymentDate { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentMethod PaymentStatus { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? ChequeNumber { get; set; }
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public DateOnly? ChequeDate { get; set; }
    public string? UPIId { get; set; }
    public string? TransactionReference { get; set; }
    public string? Remarks { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? ModifiedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public virtual Account Account { get; set; }
    public virtual Tenant Tenant { get; set; }
}

public class BidderWithoutLoanDto
{
    public long BidId { get; set; }
    public long AccountId { get; set; }
    public string AccountNumber { get; set; }
    public long MemberId { get; set; }
    public string MemberName { get; set; }
    public string MemberPhone { get; set; }
    public string Email { get; set; }
    public decimal BidPct { get; set; }
    public decimal MonthlyContribution { get; set; }
    public decimal ForfeitureAmount { get; set; }
    public decimal PrizeIfWins { get; set; }
    public DateTime SubmittedAt { get; set; }
    public bool IsApproved { get; set; }
    public bool IsWinner { get; set; }
    public string Status { get; set; }
    public string StatusBadge { get; set; }
    public long? CycleId { get; set; }
    public DateOnly? CycleMonth { get; set; }
}