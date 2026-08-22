namespace EasyMoney.Api.Domain;

public enum TenantStatus { ACTIVE, PENDING, REJECTED,SUSPENDED }
public enum KycMode { MINIMAL_FIRST, FULL_ONLY }
public enum MemberType { INDIVIDUAL, CORPORATE }
public enum KycTier { MINIMAL, FULL }
public enum KycStatus { PENDING, UNDER_REVIEW, APPROVED, REJECTED, RE_KYC_REQUIRED }
public enum Gender { MALE, FEMALE, OTHER }
public enum GlStatus { Active, IncActive }

public enum IncomeBand { BELOW_1L, _1L_5L, _5L_10L, _10L_25L, ABOVE_25L }
public enum CorporateEntityType { PRIVATE_LTD, PUBLIC_LTD, PARTNERSHIP, LLP, PROPRIETORSHIP, TRUST, SOCIETY, OTHER }
public enum KycDocType
{
    PAN, AADHAAR, VOTER_ID, PASSPORT, DRIVING_LICENSE,
    BANK_PROOF, PHOTO, ADDRESS_PROOF, COMPANY_PAN, SIGNATURE,
    COMPANY_CIN, GST_CERTIFICATE, BOARD_RESOLUTION, OTHER
}
public enum KycDocStatus { UPLOADED, VERIFIED, REJECTED }

public enum AccountStatus { ACTIVE, PRIZED, EXITED, COMPLETED, SUSPENDED }
public enum CycleStatus { OPEN, CLOSED, RESOLVED, NO_BID }
public enum LoanStatus { ACTIVE, REPAID, PENDING , APPROVED , DEFAULTED , CANCELLED }
public enum LedgerEntryType
{
    CONTRIBUTION_DUE, PAYMENT_RECEIVED, DIVIDEND_CREDIT,
    LOAN_DISBURSEMENT, LOAN_REPAYMENT, EXIT_PENALTY,
    EXIT_REFUND, CASH_BONUS
}

public enum UserRole
{
    SIFIN_ADMIN, SIFIN_OPERATOR, SIFIN_AUTHORIZER,
    ORG_ADMIN, ORG_OPERATOR, ORG_AUTHORIZER,
    MEMBER, AUDITOR
}

public enum GlAccountClass { ASSET, LIABILITY, EQUITY, INCOME, EXPENSE }

public enum JournalSourceType
{
    CONTRIBUTION, BID_RESOLUTION, LOAN_DISBURSEMENT,
    LOAN_REPAYMENT, DIVIDEND, EXIT, MANUAL_ADJUSTMENT
}

public enum PaymentMethod { CASH, BANK_TRANSFER, EFT, NEFT, RTGS, UPI, CHEQUE, SYSTEM }

public enum EntryTarget { GL, MEMBER_ACCOUNT }

public enum ApprovalActionType
{
    TENANT_CREATE, TENANT_STATUS_CHANGE, SCHEME_CONFIG_UPDATE, 
    USER_CREATE, USER_ROLE_CHANGE, ACCOUNT_OPEN,
    LOAN_DISBURSEMENT, CYCLE_RESOLUTION_OVERRIDE,
    MANUAL_JOURNAL_ADJUSTMENT, EXIT_PROCESS, MEMBER_KYC_APPROVAL,BID_APPROVE, CREATE_LOAN
}
public enum ApprovalStatus { PENDING, APPROVED, REJECTED, CANCELLED, AUTO_APPROVED }
public enum BranchStatus
{
    ACTIVE=1,
    INACTIVE=2
}

public static class SystemGl
{
    public const string CashInHand = "1001";
    public const string Bank = "1001";
    public const string EftClearing = "1030";
    public const string LoansReceivable = "1001";
    public const string ContributionsReceivable = "1200";
    public const string BiddingPoolClearing = "1300";
    public const string MemberContributionsPayable = "2000";
    public const string DividendsPayable = "2100";
    public const string SifinCommissionPayable = "3003";
    public const string OrgFeeIncome = "3001";
    public const string PenaltyIncome = "4100";
    public const string SifinPlatformCommissionIncome = "4200";
    public const string DividendExpense = "5000";
}

public static class NotificationTypes
{
    public const string PaymentDueReminder = "PAYMENT_DUE_REMINDER";
    public const string PaymentReceived    = "PAYMENT_RECEIVED";
    public const string CycleOpened        = "CYCLE_OPENED";
    public const string CycleResolved      = "CYCLE_RESOLVED";
    public const string KycStatusChanged   = "KYC_STATUS_CHANGED";
    public const string EarlyExitProcessed = "EARLY_EXIT_PROCESSED";
}

public class DomainException : Exception { public DomainException(string message) : base(message) { } }
public class UnbalancedJournalException : DomainException { public UnbalancedJournalException(string message) : base(message) { } }


public enum ReportsType
{
    KycReports = 1,  
    CustomerAccountReport = 2,
    AccountsTranactionReports = 3,
    GetChartOfAccounts = 4,
    GeneralLedegerReport =5,   
}
