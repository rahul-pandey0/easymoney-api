public record SchemeSummaryDto(
    long TenantId,

    // Tenant Details
    string? SchemeName,
    string? Address,
    string? ContactNumber,
    string? Email,

    // Basic
    int TenureMonths,
    decimal OrgFeePct,
    decimal SifinCommissionPct,
    decimal MinBidPct,
    decimal MaxBidPct,
    decimal EarlyExitPenaltyPct,
    int MinInstallmentsForEligibility,
    int BiddingWindowOpenDay,
    int BiddingDayOfMonth,
    decimal NoBidDefaultDividendPct,

    string KycMode,
    bool MakerCheckerEnabled,

    // Bank Details
    string? BankName,
    string? CustAddress1,
    string? CustAddress2,
    string? CustAddress3,
    string? PhNum,
    string? RdStatus,

    // Bonus
    decimal GrossBonus,
    decimal TenantCommission,
    decimal NetBonus,

    // GL Accounts
    string? Reserve1,
    string? Reserve2,
    string? PoolMoney,
    string? TenantPin,
    string? LoanAssetGL,
    string? SifinPayable,

    // Time Change
    decimal TimeChPass,

    // Penalty
    string? PenaltyAcc,
    string? NMPenaltyAcc,

    // Interest
    decimal MinimumRate,
    decimal MaximumRate,
    int MinimumPeriod,
    int MaximumPeriod,

    // Tax
    string? TdsAc,
    string? ServicesTax,

    // Audit
    long? UpdatedBy,
    DateTime? UpdatedAt,
    long? AuthorizedBy,
    DateTime? AuthorizedAt
);