using EasyMoney.Api.Domain;
using System.Collections.Generic;

namespace EasyMoney.Api.Dtos;

// ============================================================
// Input — posting a journal
// ============================================================
public record JournalLineInput(
    EntryTarget Target,
    string? GlAccountCode,      // required if Target = GL
    long? MemberAccountId,      // required if Target = MEMBER_ACCOUNT
    decimal Debit,
    decimal Credit);

public record PostJournalRequest(
    DateOnly EntryDate,
    JournalSourceType SourceType,
    long? SourceId,
    PaymentMethod PaymentMethod,
    string Description,
    IReadOnlyList<JournalLineInput> Lines);

public record PostJournalResult(long JournalId, int LineCount, decimal TotalDebit, decimal TotalCredit);

// ============================================================
// Read — chart of accounts
// ============================================================
public record GlAccountDto(
    long GlAccountId,
    string Code,
    string Name,
    string AccountClass,
    string? ParentCode,
    bool IsActive,
    decimal Balance,
    bool? Forbank,
    bool? IsReported,
    bool? HasTransactions,
    bool? HasGst,
    long TenantId
      );


    //string? AccountNumber,
    //string? Phone);

// ============================================================
// Read — GL ledger (per-GL-account journal lines)
// ============================================================
public record GlLedgerLineDto(
    long JournalId,
    DateOnly EntryDate,
    string SourceType,
    string PaymentMethod,
    string? Description,
    long? MemberAccountId,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance);

public record GlLedgerDto(
    string Code,
    string Name,
    string AccountClass,
    DateOnly? From,
    DateOnly? To,
    decimal OpeningBalance,
    decimal ClosingBalance,
    decimal TotalDebit,
    decimal TotalCredit,
    IReadOnlyList<GlLedgerLineDto> Lines);

// ============================================================
// Read — member-account GL ledger (per-Account journal lines)
// ============================================================
public record MemberAccountLedgerLineDto(
    long JournalId,
    DateOnly EntryDate,
    string SourceType,
    string PaymentMethod,
    string? Description,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance);

public record MemberAccountLedgerDto(
    long AccountId,
    string AccountNumber,
    string? Customername,
    DateOnly? From,
    DateOnly? To,
    decimal OpeningBalance,
    decimal ClosingBalance,
    decimal TotalDebit,
    decimal TotalCredit,
    IReadOnlyList<MemberAccountLedgerLineDto> Lines);

// ============================================================
// Read — trial balance / balance sheet / income statement
// ============================================================
public record TrialBalanceLineDto(
    string Code,
    string Name,
    string AccountClass,
    decimal Debit,
    decimal Credit);

public record TrialBalanceDto(
    DateOnly AsOf,
    decimal TotalDebit,
    decimal TotalCredit,
    bool Balanced,
    IReadOnlyList<TrialBalanceLineDto> Lines);

public record BalanceSheetGroupDto(string AccountClass, decimal Total, IReadOnlyList<TrialBalanceLineDto> Lines);

public record BalanceSheetDto(
    DateOnly AsOf,
    BalanceSheetGroupDto Assets,
    BalanceSheetGroupDto Liabilities,
    BalanceSheetGroupDto Equity,
    decimal RetainedEarnings,   // net income from inception to AsOf
    bool Balanced);

public record IncomeStatementDto(
    DateOnly From,
    DateOnly To,
    BalanceSheetGroupDto Income,
    BalanceSheetGroupDto Expense,
    decimal NetIncome);


public sealed record PoolMoneySummary(
    decimal PoolMoney,
    decimal TargetAmount,
    DateOnly MonthStart,
    DateOnly MonthEnd
);

