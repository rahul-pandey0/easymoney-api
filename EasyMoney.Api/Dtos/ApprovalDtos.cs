using EasyMoney.Api.Domain;

namespace EasyMoney.Api.Dtos;

// ============================================================
// Generic submit/decide
// ============================================================
public record CreateApprovalRequest(
    ApprovalActionType ActionType,
    string EntityType,
    long? EntityId,
    object Payload);                  // serialized to JSON; per-action shape

public record ApprovalDecisionRequest(
    bool Approve,                     // true = APPROVED, false = REJECTED
    string? Remarks);

public record ApprovalRequestDto(
    long RequestId,
    long? TenantId,
    string ActionType,
    string EntityType,
    long? EntityId,
    string Payload,                   // raw JSON string
    string Status,
    long RequestedBy,
    DateTime RequestedAt,
    long? DecidedBy,
    DateTime? DecidedAt,
    string? DecisionRemarks);

// ============================================================
// Strongly-typed payloads for each action_type
// (deserialized inside the service when applying an APPROVED request)
// ============================================================
public record TenantCreatePayload(
    string Name,
    string? RegistrationNumber,
    string? Address,
    string? Phone,
    string? OrgEmail,
    string? ContactPersonName,
    string? ContactPersonPhone,
    DateOnly? StartDate,
    DateOnly? EffectiveDate);

public record TenantStatusChangePayload(string Status);   // ACTIVE | SUSPENDED

public record SchemeConfigUpdatePayload(
    int? TenureMonths,
    decimal? OrgFeePct,
    decimal? SifinCommissionPct,
    decimal? MinBidPct,
    decimal? MaxBidPct,
    decimal? EarlyExitPenaltyPct,
    int? MinInstallmentsForEligibility,
    int? BiddingWindowOpenDay,
    int? BiddingDayOfMonth,
    decimal? NoBidDefaultDividendPct,
    string? KycMode,                   // MINIMAL_FIRST | FULL_ONLY
    bool? MakerCheckerEnabled,
    // Bank Details
    string? BankName,
    string? CustAddress1,
    string? CustAddress2,
    string? CustAddress3,
    string? Email,
    string? PhNum,
    //string? Fax,
    string? RdStatus,

    // Bonus / Commission
    decimal? GrossBonus,
    decimal? TenantCommission,
    decimal? NetBonus,

    // GL / Reserve
    string? Reserve1,
    string? Reserve2,
    string? PoolMoney,
    string? TenantPin,
    string? LoanAssetGL,
    string? SifinPayable,

    // Time Change
    decimal? TimeChPass,

    // Penalty
    string? PenaltyAcc,
    string? NMPenaltyAcc,

    // Interest
    decimal? MinimumRate,
    decimal? MaximumRate,
    int? MinimumPeriod,
    int? MaximumPeriod,

    // Tax
    string? TdsAc,
    string? ServicesTax,
    string? SchemeName,
decimal? FixedRate,
string? GstGl,
string? SchemeCode,
    int ? OrgFeeGlId, 
        int? SifinCommissionGlId,
        decimal? MinimumInstallmentAmount,
        decimal ? MaximumInstallmentAmount



);



public record UserCreatePayload(
    long? TenantId,
    string Email,
    string Password,
    string Role,
    long? MemberId,
    long? BranchId,
        string? UserName,
    string? HintAnswerHash, string? HintQuestion

    );

public record UserRoleChangePayload(string NewRole);

public record UserStatusChangePayload(bool IsActive);

public record AccountOpenPayload(
    long MemberId,
    decimal MonthlyContribution,
    DateOnly AccountOpenDate,

       // New fields from frontend
    string? OldAccountNo,
    string? PhoneNo,
    string? CustomerName,
    decimal InterestRate,
    decimal TargetAmount,
    DateOnly? PaymentDate,
    decimal PaidAmount,
    decimal LoanAmount,
    decimal BonusAmount,
    decimal InterestAmount,
    decimal TotalAmount,
    string? Remarks,
    int SchemeId, 
    long ? BranchId,
    string ? IsBidding,
    string ?FirstPayment,
    string? CustomerCode
    );

public record AccountUpdatePayload(
    long MemberId,
    decimal MonthlyContribution,
    DateOnly AccountOpenDate,
    string AccountNumber,
    string? OldAccountNo,
    string? PhoneNo,
    string? CustomerName,
    decimal InterestRate,
    decimal TargetAmount,
    DateOnly? PaymentDate,
    decimal PaidAmount,
    decimal LoanAmount,
    decimal BonusAmount,
    decimal InterestAmount,
    decimal TotalAmount,
    string? Remarks,
    int? SchemeId,
    DateOnly TenureEndDate,
    string? Status,
    int? InstallmentsPaid,
    bool? IsPrized,
    DateOnly? ClosureDate

);


public record MemberKycApprovalPayload(
    long MemberId,
    string ToStatus,    // APPROVED | REJECTED | RE_KYC_REQUIRED
    string? Remarks);

public record LoanDisbursementPayload(
    long AccountId,
    long CycleId,
    decimal Principal);

public record CycleResolutionOverridePayload(long CycleId);

public record BIDapprove(long CycleId);


// Use a complete DTO that matches your payload
public record BidApprovalPayload(
    long BidId,
    long CycleId,
    long AccountId,
    decimal BidPct,
    bool IsApproved,
    long TenantId,
    long RequestedBy
);

public record ExitProcessPayload(
    long AccountId,
    DateOnly ExitDate,
    string RefundMethod);

public record ManualJournalAdjustmentPayload(
    long TenantId,
    DateOnly EntryDate,
    string PaymentMethod,
    string Description,
    IReadOnlyList<JournalLineInput> Lines);


// EasyMoney.Api.Dtos/BidApprovalPayloadDto.cs
public class BidApprovalPayloadDto
{
    public long BidId { get; set; }
    public long CycleId { get; set; }
    public long AccountId { get; set; }
    public decimal BidPct { get; set; }
    public bool IsApproved { get; set; }
    public long TenantId { get; set; }
    public long RequestedBy { get; set; }
}