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
    string? ContactPersonPhone);

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
    bool? MakerCheckerEnabled);

public record UserCreatePayload(
    long? TenantId,
    string Email,
    string Password,
    string Role,
    long? MemberId);

public record UserRoleChangePayload(string NewRole);

public record UserStatusChangePayload(bool IsActive);

public record AccountOpenPayload(
    long MemberId,
    decimal MonthlyContribution,
    DateOnly AccountOpenDate);

public record MemberKycApprovalPayload(
    long MemberId,
    string ToStatus,    // APPROVED | REJECTED | RE_KYC_REQUIRED
    string? Remarks);

public record LoanDisbursementPayload(
    long AccountId,
    long CycleId,
    decimal Principal);

public record CycleResolutionOverridePayload(long CycleId);

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
