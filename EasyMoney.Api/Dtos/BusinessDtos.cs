using EasyMoney.Api.Domain;

namespace EasyMoney.Api.Dtos;

// ============================================================
// Account
// ============================================================
public record OpenAccountRequest(decimal MonthlyContribution, DateOnly AccountOpenDate ,  long MemberId,
    string? OldAccountNo, string? PhoneNo,string? CustomerName, decimal InterestRate,decimal TargetAmount,
    DateOnly? PaymentDate,decimal PaidAmount, decimal LoanAmount, decimal BonusAmount, decimal InterestAmount,
    decimal TotalAmount, string? Remarks,int  SchemeId,long BranchId);
 
//public record UpdateAccountRequest(string accountNumber,decimal MonthlyContribution, DateOnly AccountOpenDate, long MemberId,
//    string? OldAccountNo, string? PhoneNo, string? CustomerName, decimal InterestRate, decimal TargetAmount,
//    DateOnly? PaymentDate, decimal PaidAmount, decimal LoanAmount, decimal BonusAmount, decimal InterestAmount,
//    decimal TotalAmount, string? Remarks, int SchemeId, DateOnly TenureEndDate);


public record UpdateAccountRequest(
    string AccountNumber,
    decimal MonthlyContribution,
    DateOnly AccountOpenDate,
     long MemberId,
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
    DateOnly TenureEndDate,
    string? Status,
    int? InstallmentsPaid,
    bool? IsPrized,
    DateOnly? ClosureDate,
    DateOnly ? UpdateDate,
    int ? updtaeBy 
);

public record AccountSummaryDto(
    long AccountId, string AccountNumber, long MemberId, long TenantId,
    decimal MonthlyContribution, DateOnly AccountOpenDate, DateOnly TenureEndDate,
    string Status, int InstallmentsPaid, bool IsPrized,
    bool IsEligibleToBid, bool IsEligibleForDividend,
    decimal CorpusBalance, long? PrizeWonInCycleId,
    DateTime CreatedAt, string OldAccountNo, string PhoneNo, string CustomerName, decimal InterestRate, decimal TargetAmount,
    DateOnly? PaymentDate, decimal PaidAmount, decimal LoanAmount,decimal bonusAmount, decimal InterestAmount,
        decimal TotalAmount, string Remarks, int? SchemeId , long? BranchId , string ? IsBidding); 

// ============================================================
// Payment / Ledger
// ============================================================
public record DueLineDto(
    long DueId,
    DateOnly DueMonth,
    decimal AmountDue,
    decimal AmountPaid,
    decimal Balance,
    string Status);   // PAID | UPCOMING | OVERDUE


public record RecordPaymentRequest(
    decimal Amount,
    DateOnly PaidDate,
    string Method,     // CASH / BANK_TRANSFER / UPI / etc
    long? DueId,
    string? GlCode 
      
    );

//public record RecordPaymentRequest(
//        decimal Amount,

//    decimal InstallmentAmount,
//    DateOnly PaidDate,
//    string Method,     // CASH / BANK_TRANSFER / UPI / etc
//    long? DueId,
//    decimal PenaltyAmount,
//    decimal OtherCharges,
//    decimal TotalAmount,
//    bool ClosurePayment,
//    long? GlAccountId,
//    string? GlAccountName,
//    string? PhoneNumber,
//    string? VoucherNo,
//    string? Remarks);      // optional: pay against a specific due line

public record PaymentResultDto(
    long AccountId, decimal AmountPaid, int InstallmentsPaid,
    decimal CorpusBalance, long JournalId);

// ============================================================
// Bidding
// ============================================================
public record CycleDto(
    long CycleId, long TenantId, DateOnly CycleMonth,
    DateTime WindowOpenAt, DateTime WindowCloseAt,
    string Status,
    decimal? GrossCorpus, decimal? OrgFeeAmount, decimal? BidPool,
    long? WinnerAccountId, decimal? WinnerBidPct,
    decimal? LoanDisbursed, decimal? DividendPool,
    DateTime? ResolvedAt);

public record SubmitBidRequest(decimal BidPct);
public record BidDto(long BidId, long CycleId, long AccountId, decimal BidPct, DateTime SubmittedAt, DateTime? UpdatedAt, bool IsWinner);

// Ranked bid line shown in the award preview
public record AwardPreviewBidDto(
    int Rank,
    long BidId,
    long AccountId,
    string AccountNumber,
    long MemberId,
    string MemberName,
    string MemberPhone,
    decimal BidPct,
    decimal ForfeitureAmount,   // grossCorpus * bidPct / 100
    decimal PrizeIfWins,        // bidPool - forfeitureAmount
    DateTime SubmittedAt,
    DateTime? UpdatedAt);

// Full pre-award review shown before resolving
public record AwardPreviewDto(
    long CycleId,
    DateOnly CycleMonth,
    string Status,
    decimal GrossCorpus,
    decimal OrgFeeAmount,
    decimal BidPool,
    int TotalBids,
    IReadOnlyList<AwardPreviewBidDto> BidsRanked);  // highest bidPct first

public record CycleResolutionResultDto(
    long CycleId, string Status, decimal GrossCorpus, decimal OrgFeeAmount, decimal BidPool,
    long? WinnerAccountId, decimal? WinnerBidPct, decimal LoanDisbursed, decimal DividendPool,
    int DividendCount, decimal SifinShare);

// ============================================================
// Loan (prize record — member won the bid in this cycle)
// ============================================================
public record LoanDto(
    long LoanId,
    long AccountId,
    long CycleId,
    decimal PrizeAmount,      // cash received by the winner = bidPool - forfeiture
    DateTime DisbursedAt);

// ============================================================
// Notifications
// ============================================================
public record NotificationDto(
    long NotificationId,
    long TenantId,
    long? UserId,
    long? AccountId,
    string Type,
    string Title,
    string Body,
    bool IsRead,
    DateTime? ReadAt,
    DateTime CreatedAt);

public record NotificationPageDto(int Total, int Unread, int Skip, int Take, IReadOnlyList<NotificationDto> Items);

// ============================================================
// Audit Log
// ============================================================
public record AuditLogDto(
    long AuditId,
    long? TenantId,
    long? UserId,
    string Action,
    string EntityType,
    long EntityId,
    string? OldValue,
    string? NewValue,
    DateTime CreatedAt);

public record AuditLogPageDto(
    int Total,
    int Skip,
    int Take,
    IReadOnlyList<AuditLogDto> Items);

// ============================================================
// Exit
// ============================================================
public record ExitResultDto(
    long AccountId, DateOnly ExitDate,
    decimal TotalPaidIn, decimal Penalty, decimal RefundAmount,
    long? PenaltyJournalId, long? RefundJournalId);
