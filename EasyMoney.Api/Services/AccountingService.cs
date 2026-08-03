using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EasyMoney.Api.Services;

/// <summary>
/// Double-entry general ledger service.
/// All financial events MUST post a balanced journal via PostJournalAsync.
/// </summary>
public interface IAccountingService
{
    // ---- Posting ----
    Task<long> PostJournalAsync(
        long tenantId,
        DateOnly entryDate,
        JournalSourceType sourceType,
        long? sourceId,
        PaymentMethod paymentMethod,
        string description,
        IReadOnlyList<JournalLineInput> lines,
        long? createdBy = null,
        long? authorizedBy = null);

    // ---- Chart of Accounts ----
    Task SeedTenantChartAsync(long tenantId, long? createdBy);
    Task<long> EnsureGlAccountAsync(long tenantId, string code, string name, GlAccountClass cls, string? parentCode, long? createdBy);
    Task<IReadOnlyList<GlAccountDto>> GetChartOfAccountsAsync(long tenantId);

    // ---- Balance reads ----
    Task<decimal> GetGlBalanceAsync(long tenantId, string code);
    Task<decimal> GetMemberAccountBalanceAsync(long accountId);

    // ---- Ledger / report reads ----
    Task<GlLedgerDto> GetGlLedgerAsync(long tenantId, string code, DateOnly? fromDate, DateOnly? toDate);
    Task<MemberAccountLedgerDto> GetMemberAccountLedgerAsync(long accountId, DateOnly? fromDate, DateOnly? toDate);

     

    Task<List<MemberAccountLedgerDto>> GetMemberAccountLedgerDetailsAsync(long accountId, DateOnly? fromDate, DateOnly? toDate);
    Task<MemberAccountLedgerDto> GetMemberAccountLedgerDetailsAsync(long JournalId); 


    Task<TrialBalanceDto> GetTrialBalanceAsync(long tenantId, DateOnly asOf);
    Task<BalanceSheetDto> GetBalanceSheetAsync(long tenantId, DateOnly asOf);
    Task<IncomeStatementDto> GetIncomeStatementAsync(long tenantId, DateOnly from, DateOnly to);

    Task<IEnumerable<PaymentReportDto>> GetPaymentReportAsync(
            long? tenantId = null,    
            long? branchId = null,
            string? type = null,
            DateOnly? fromDate = null,
            DateOnly? toDate = null
        );

    //Task<IEnumerable<AccountOpenReportDto>> GetAccountOpenReportAsync(long tenantId, DateOnly? fromDate, DateOnly? toDate);
    //Task<IEnumerable<KycReportDto>> GetKycReportAsync( long tenantId, long? branchId, string? type, DateOnly? fromDate, DateOnly? toDate);


    Task<IEnumerable<AccountOpenReportDto>> GetAccountOpenReportAsync(
            long? tenantId = null,
            long? branchId = null,
            string? type = null,
            DateOnly? fromDate = null,
            DateOnly? toDate = null
        );

    Task<IEnumerable<KycReportDto>> GetKycReportAsync(
          long? tenantId = null,     // Nullable - handles both scenarios
          long? branchId = null,
          string? type = null,
          DateOnly? fromDate = null,
          DateOnly? toDate = null
      );


}

public class AccountingService : IAccountingService
{
    private readonly EasyMoneyDbContext _db;
    private readonly ITenantContext _tenantCtx;
    private readonly ILogger<AccountingService> _log;

    public AccountingService(EasyMoneyDbContext db, ITenantContext tenantCtx, ILogger<AccountingService> log)
    {
        _db = db; _tenantCtx = tenantCtx; _log = log;
    }

    // ===========================================================
    // Posting
    // ===========================================================

    public async Task<long> PostJournalAsync(
        long tenantId,
        DateOnly entryDate,
        JournalSourceType sourceType,
        long? sourceId,
        PaymentMethod paymentMethod,
        string description,
        IReadOnlyList<JournalLineInput> lines,
        long? createdBy = null,
        long? authorizedBy = null)
    {
        if (lines is null || lines.Count < 2)
            throw new DomainException("Journal must have at least 2 lines (one debit, one credit)");

        // ---- 1. Validate each line shape (exactly one of GL/MEMBER_ACCOUNT, exactly one of debit/credit) ----
        foreach (var l in lines)
        {
            if (l.Debit < 0 || l.Credit < 0)
                throw new DomainException("Debit/Credit must be non-negative");
            if ((l.Debit > 0 && l.Credit > 0) || (l.Debit == 0 && l.Credit == 0))
                throw new DomainException("Each line must have exactly one of debit or credit (> 0)");

            if (l.Target == EntryTarget.GL)
            {
                if (string.IsNullOrWhiteSpace(l.GlAccountCode))
                    throw new DomainException("GL line requires glAccountCode");
                if (l.MemberAccountId.HasValue)
                    throw new DomainException("GL line must not have memberAccountId");
            }
            else if (l.Target == EntryTarget.MEMBER_ACCOUNT)
            {
                if (!l.MemberAccountId.HasValue)
                    throw new DomainException("MEMBER_ACCOUNT line requires memberAccountId");
                if (!string.IsNullOrWhiteSpace(l.GlAccountCode))
                    throw new DomainException("MEMBER_ACCOUNT line must not have glAccountCode");
            }
            else
            {
                throw new DomainException($"Unknown entry_target {l.Target}");
            }
        }

        // ---- 2. Balanced check ----
        var totalDebit = lines.Sum(l => l.Debit);
        var totalCredit = lines.Sum(l => l.Credit);
        if (Math.Round(totalDebit, 2) != Math.Round(totalCredit, 2))
            throw new UnbalancedJournalException($"Unbalanced journal: debit={totalDebit}, credit={totalCredit}");

        // ---- 3. Resolve GL codes to ids (and verify they belong to this tenant) ----
        var glCodes = lines.Where(l => l.Target == EntryTarget.GL)
            .Select(l => l.GlAccountCode!).Distinct().ToList();
        var glMap = await _db.GlAccounts.IgnoreQueryFilters()
            .Where(g => g.TenantId == tenantId && glCodes.Contains(g.Code))
            .ToDictionaryAsync(g => g.Code, g => new { g.GlAccountId, g.AccountClass });

        foreach (var code in glCodes)
            if (!glMap.ContainsKey(code))
                throw new DomainException($"GL account '{code}' not found for tenant {tenantId}");

        // ---- 4. Verify member accounts belong to this tenant ----
        var memberAccountIds = lines.Where(l => l.Target == EntryTarget.MEMBER_ACCOUNT)
            .Select(l => l.MemberAccountId!.Value).Distinct().ToList();
        if (memberAccountIds.Count > 0)
        {
            var validCount = await _db.Accounts.IgnoreQueryFilters()
                .CountAsync(a => a.TenantId == tenantId && memberAccountIds.Contains(a.AccountId));
            if (validCount != memberAccountIds.Count)
                throw new DomainException("One or more member accounts do not belong to this tenant");
        }

        // ---- 5. Manual-adjustment journals MUST have an authorizer ----
        if (sourceType == JournalSourceType.MANUAL_ADJUSTMENT && !authorizedBy.HasValue)
            throw new DomainException("MANUAL_ADJUSTMENT journals require an authorizer");

        // ---- 6. Post inside a transaction, wrapped in EF's retrying execution strategy ----
        long journalId = 0;
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            var je = new JournalEntry
            {
                TenantId = tenantId,
                EntryDate = entryDate,
                SourceType = sourceType,
                SourceId = sourceId,
                PaymentMethod = paymentMethod,
                Description = description,
                CreatedBy = createdBy ?? _tenantCtx.UserId,
                AuthorizedBy = authorizedBy,
                AuthorizedAt = authorizedBy.HasValue ? DateTime.UtcNow : null
            };
            _db.JournalEntries.Add(je);
            await _db.SaveChangesAsync();    // assigns JournalId

            // Cache balances we'll touch (single read per gl_account / per member_account).
            var glBalances = new Dictionary<long, GlAccountBalance>();
            var memberBalances = new Dictionary<long, MemberAccountBalance>();

            foreach (var l in lines)
            {
                var line = new JournalLine
                {
                    JournalId = je.JournalId,
                    EntryTarget = l.Target,
                    Debit = l.Debit,
                    Credit = l.Credit
                };

                if (l.Target == EntryTarget.GL)
                {
                    var gl = glMap[l.GlAccountCode!];
                    line.GlAccountId = gl.GlAccountId;

                    if (!glBalances.TryGetValue(gl.GlAccountId, out var bal))
                    {
                        bal = await _db.GlAccountBalances.FirstOrDefaultAsync(b => b.GlAccountId == gl.GlAccountId);
                        if (bal is null)
                        {
                            bal = new GlAccountBalance { GlAccountId = gl.GlAccountId, Balance = 0m };
                            _db.GlAccountBalances.Add(bal);
                        }
                        glBalances[gl.GlAccountId] = bal;
                    }

                    // Normal-side convention:
                    //   ASSET/EXPENSE:                 balance += debit - credit
                    //   LIABILITY/INCOME/EQUITY:       balance += credit - debit
                    var delta = gl.AccountClass is GlAccountClass.ASSET or GlAccountClass.EXPENSE
                        ? l.Debit - l.Credit
                        : l.Credit - l.Debit;
                    bal.Balance = Math.Round(bal.Balance + delta, 2);
                    bal.UpdatedAt = DateTime.UtcNow;
                    line.RunningBalance = bal.Balance;
                }
                else // MEMBER_ACCOUNT
                {
                    var acctId = l.MemberAccountId!.Value;
                    line.MemberAccountId = acctId;

                    if (!memberBalances.TryGetValue(acctId, out var bal))
                    {
                        bal = await _db.MemberAccountBalances.FirstOrDefaultAsync(b => b.AccountId == acctId);
                        if (bal is null)
                        {
                            bal = new MemberAccountBalance { AccountId = acctId, Balance = 0m };
                            _db.MemberAccountBalances.Add(bal);
                        }
                        memberBalances[acctId] = bal;
                    }

                    // Member account is a LIABILITY-style subsidiary ledger:
                    //   credit (+) = member's corpus grows (scheme owes them)
                    //   debit  (-) = member's corpus reduces (drawdown / penalty)
                    // So balance += credit - debit (positive = owed TO member).
                    var delta = l.Credit - l.Debit;
                    bal.Balance = Math.Round(bal.Balance + delta, 2);
                    bal.UpdatedAt = DateTime.UtcNow;
                    line.RunningBalance = bal.Balance;
                }

                _db.JournalLines.Add(line);
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            journalId = je.JournalId;
        });

        _log.LogInformation("Posted journal {Jid} for tenant {Tid}, source={Source}, total={Total}",
            journalId, tenantId, sourceType, totalDebit);
        return journalId;
    }

    // ===========================================================
    // Chart of Accounts
    // ===========================================================

    public async Task<long> EnsureGlAccountAsync(long tenantId, string code, string name, GlAccountClass cls, string? parentCode, long? createdBy)
    {
        var existing = await _db.GlAccounts.IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.TenantId == tenantId && g.Code == code);
        if (existing is not null) return existing.GlAccountId;

        var gl = new GlAccount
        {
            TenantId = tenantId,
            Code = code,
            Name = name,
            AccountClass = cls,
            ParentCode = parentCode,
            IsActive = true,
            CreatedBy = createdBy,
            AuthorizedBy = createdBy,           // seeded CoA rows are pre-authorized (system)
            AuthorizedAt = DateTime.UtcNow
        };
        _db.GlAccounts.Add(gl);
        await _db.SaveChangesAsync();
        return gl.GlAccountId;
    }

    public async Task SeedTenantChartAsync(long tenantId, long? createdBy)
    {
        // Default Chart of Accounts (per design §3.2)
        var coa = new (string code, string name, GlAccountClass cls)[]
        {
            (SystemGl.CashInHand,                  "Cash in Hand",                          GlAccountClass.ASSET),
            (SystemGl.Bank,                        "Bank Account",                          GlAccountClass.ASSET),
            (SystemGl.EftClearing,                 "EFT / Online Clearing",                 GlAccountClass.ASSET),
            (SystemGl.LoansReceivable,             "Loans Receivable (Member Loans)",       GlAccountClass.ASSET),
            (SystemGl.ContributionsReceivable,     "Contributions Receivable",              GlAccountClass.ASSET),
            (SystemGl.BiddingPoolClearing,         "Bidding Pool Clearing",                 GlAccountClass.ASSET),
            (SystemGl.MemberContributionsPayable,  "Member Contributions Payable (Corpus)", GlAccountClass.LIABILITY),
            (SystemGl.DividendsPayable,            "Dividends Payable",                     GlAccountClass.LIABILITY),
            (SystemGl.SifinCommissionPayable,      "SIFIN Commission Payable",              GlAccountClass.LIABILITY),
            (SystemGl.OrgFeeIncome,                "Organization Fee Income (Cooperative)", GlAccountClass.INCOME),
            (SystemGl.PenaltyIncome,               "Penalty Income (Early Exit)",           GlAccountClass.INCOME),
            (SystemGl.SifinPlatformCommissionIncome, "SIFIN Platform Commission Income",    GlAccountClass.INCOME),
            (SystemGl.DividendExpense,             "Dividend Expense",                      GlAccountClass.EXPENSE),
        };
        foreach (var (code, name, cls) in coa)
            await EnsureGlAccountAsync(tenantId, code, name, cls, parentCode: null, createdBy);
    }

    public async Task<IReadOnlyList<GlAccountDto>> GetChartOfAccountsAsync(long tenantId)
    {
        var rows = await (
            from g in _db.GlAccounts.IgnoreQueryFilters().Where(g => g.TenantId == tenantId)
            join b in _db.GlAccountBalances on g.GlAccountId equals b.GlAccountId into bj
            from b in bj.DefaultIfEmpty()
            orderby g.Code
            select new GlAccountDto(
                g.GlAccountId, g.Code, g.Name, g.AccountClass.ToString(),
                g.ParentCode, g.IsActive, b == null ? 0m : b.Balance,true,true,true,true, tenantId)
        ).ToListAsync();
        return rows;
    }

    //public async Task<IReadOnlyList<GlAccountDto>> GetChartOfAccountsAsync(long tenantId)
    //{
    //    var rows = await (
    //        from g in _db.GlAccounts.IgnoreQueryFilters()
    //            .Where(g => g.TenantId == tenantId)

    //        join b in _db.GlAccountBalances
    //            on g.GlAccountId equals b.GlAccountId into bj
    //        from b in bj.DefaultIfEmpty()

    //            // Get latest journal line for this GL account
    //        join jl in _db.JournalLines
    //            on g.GlAccountId equals jl.GlAccountId into jlg
    //        from jl in jlg.OrderByDescending(x => x.LineId).Take(1).DefaultIfEmpty()

    //        join a in _db.Accounts
    //            on jl.MemberAccountId equals a.AccountId into ag
    //        from a in ag.DefaultIfEmpty()

    //        join m in _db.Members
    //            on a.MemberId equals m.MemberId into mg
    //        from m in mg.DefaultIfEmpty()

    //        orderby g.Code

    //        select new GlAccountDto(
    //            g.GlAccountId,
    //            g.Code,
    //            g.Name,
    //            g.AccountClass.ToString(),
    //            g.ParentCode,
    //            g.IsActive,
    //            b == null ? 0m : b.Balance,
    //            a == null ? null : a.AccountNumber,
    //            m == null ? null : m.Phone
    //        )
    //    ).ToListAsync();

    //    return rows;
    //}

    // ===========================================================
    // Balance reads
    // ===========================================================

    public async Task<decimal> GetGlBalanceAsync(long tenantId, string code)
    {
        var gl = await _db.GlAccounts.IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.TenantId == tenantId && g.Code == code);
        if (gl is null) return 0m;
        var bal = await _db.GlAccountBalances.FirstOrDefaultAsync(b => b.GlAccountId == gl.GlAccountId);
        return bal?.Balance ?? 0m;
    }

    public async Task<decimal> GetMemberAccountBalanceAsync(long accountId)
    {
        var bal = await _db.MemberAccountBalances.FirstOrDefaultAsync(b => b.AccountId == accountId);
        return bal?.Balance ?? 0m;
    }

    // ===========================================================
    // Ledger reads
    // ===========================================================

    public async Task<GlLedgerDto> GetGlLedgerAsync(long tenantId, string code, DateOnly? fromDate, DateOnly? toDate)
    {
        var gl = await _db.GlAccounts.IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.TenantId == tenantId && g.Code == code)
            ?? throw new DomainException($"GL account '{code}' not found for tenant {tenantId}");

        // Opening balance = sign-applied sum of all postings strictly before `fromDate`.
        decimal opening = 0m;
        var jl = _db.JournalLines.IgnoreQueryFilters();
        var je = _db.JournalEntries.IgnoreQueryFilters();
        if (fromDate.HasValue)
        {
            var fd = fromDate.Value;
            var prior = await (
                from l in jl
                join j in je on l.JournalId equals j.JournalId
                where j.TenantId == tenantId && l.GlAccountId == gl.GlAccountId && j.EntryDate < fd
                select new { l.Debit, l.Credit }
            ).ToListAsync();
            var pd = prior.Sum(x => x.Debit);
            var pc = prior.Sum(x => x.Credit);
            opening = gl.AccountClass is GlAccountClass.ASSET or GlAccountClass.EXPENSE ? pd - pc : pc - pd;
            opening = Math.Round(opening, 2);
        }

        var q =
            from l in jl
            join j in je on l.JournalId equals j.JournalId
            where j.TenantId == tenantId && l.GlAccountId == gl.GlAccountId
            select new { l, j };
        if (fromDate.HasValue) q = q.Where(x => x.j.EntryDate >= fromDate.Value);
        if (toDate.HasValue)   q = q.Where(x => x.j.EntryDate <= toDate.Value);
        var rows = await q.OrderBy(x => x.j.EntryDate).ThenBy(x => x.l.LineId).ToListAsync();

        var sign = gl.AccountClass is GlAccountClass.ASSET or GlAccountClass.EXPENSE ? 1 : -1;
        decimal running = opening;
        var lines = new List<GlLedgerLineDto>();
        decimal totD = 0m, totC = 0m;
        foreach (var r in rows)
        {
            running += sign * (r.l.Debit - r.l.Credit);
            running = Math.Round(running, 2);
            totD += r.l.Debit; totC += r.l.Credit;
            lines.Add(new GlLedgerLineDto(
                r.j.JournalId, r.j.EntryDate, r.j.SourceType.ToString(),
                (r.j.PaymentMethod ?? PaymentMethod.SYSTEM).ToString(),
                r.j.Description, r.l.MemberAccountId, r.l.Debit, r.l.Credit, running));
        }
        return new GlLedgerDto(gl.Code, gl.Name, gl.AccountClass.ToString(),
            fromDate, toDate, opening, running, totD, totC, lines);
    }

    public async Task<MemberAccountLedgerDto> GetMemberAccountLedgerAsync(long accountId, DateOnly? fromDate, DateOnly? toDate)
    {
        var acct = await _db.Accounts.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.AccountId == accountId)
            ?? throw new DomainException($"Account {accountId} not found");

        // Member-account "balance" treats credit (+) as money owed to the member.
        decimal opening = 0m;
        var jl = _db.JournalLines.IgnoreQueryFilters();
        var je = _db.JournalEntries.IgnoreQueryFilters();
        if (fromDate.HasValue)
        {
            var fd = fromDate.Value;
            var prior = await (
                from l in jl
                join j in je on l.JournalId equals j.JournalId
                where l.MemberAccountId == accountId && j.EntryDate < fd
                select new { l.Debit, l.Credit }
            ).ToListAsync();
            opening = Math.Round(prior.Sum(x => x.Credit - x.Debit), 2);
        }

        var q =
            from l in jl
            join j in je on l.JournalId equals j.JournalId
            where l.MemberAccountId == accountId
            select new { l, j };
        if (fromDate.HasValue) q = q.Where(x => x.j.EntryDate >= fromDate.Value);
        if (toDate.HasValue)   q = q.Where(x => x.j.EntryDate <= toDate.Value);
        var rows = await q.OrderBy(x => x.j.EntryDate).ThenBy(x => x.l.LineId).ToListAsync();

        decimal running = opening;
        decimal totD = 0m, totC = 0m;
        var lines = new List<MemberAccountLedgerLineDto>();
        foreach (var r in rows)
        {
            running += r.l.Credit - r.l.Debit;
            running = Math.Round(running, 2);
            totD += r.l.Debit; totC += r.l.Credit;
            lines.Add(new MemberAccountLedgerLineDto(
                r.j.JournalId, r.j.EntryDate, r.j.SourceType.ToString(),
                (r.j.PaymentMethod ?? PaymentMethod.SYSTEM).ToString(),
                r.j.Description, r.l.Debit, r.l.Credit, running));
        }
        return new MemberAccountLedgerDto(acct.AccountId, acct.AccountNumber,
            fromDate, toDate, opening, running, totD, totC, lines);
    }



    public async Task<List<MemberAccountLedgerDto>> GetMemberAccountLedgerDetailsAsync(long tenantId, DateOnly? fromDate, DateOnly? toDate)
    {
        var accounts = await _db.Accounts.IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId)
            .ToListAsync();

        if (!accounts.Any())
            throw new DomainException($"No accounts found for tenant {tenantId}");

        var result = new List<MemberAccountLedgerDto>();
        var jl = _db.JournalLines.IgnoreQueryFilters();
        var je = _db.JournalEntries.IgnoreQueryFilters();

        foreach (var acct in accounts)
        {
            decimal opening = 0m;

            // Opening balance for this account
            if (fromDate.HasValue)
            {
                var fd = fromDate.Value;
                var prior = await (
                    from l in jl
                    join j in je on l.JournalId equals j.JournalId
                    where j.TenantId == tenantId && l.MemberAccountId == acct.AccountId && j.EntryDate < fd
                    select new { l.Debit, l.Credit }
                ).ToListAsync();
                opening = Math.Round(prior.Sum(x => x.Credit - x.Debit), 2);
            }

            // Get transactions for this account
            var q =
                from l in jl
                join j in je on l.JournalId equals j.JournalId
                where j.TenantId == tenantId && l.MemberAccountId == acct.AccountId
                select new { l, j };

            if (fromDate.HasValue) q = q.Where(x => x.j.EntryDate >= fromDate.Value);
            if (toDate.HasValue) q = q.Where(x => x.j.EntryDate <= toDate.Value);

            var rows = await q.OrderBy(x => x.j.EntryDate).ThenBy(x => x.l.LineId).ToListAsync();

            decimal running = opening;
            decimal totD = 0m, totC = 0m;
            var lines = new List<MemberAccountLedgerLineDto>();

            foreach (var r in rows)
            {
                running += r.l.Credit - r.l.Debit;
                running = Math.Round(running, 2);
                totD += r.l.Debit; totC += r.l.Credit;
                lines.Add(new MemberAccountLedgerLineDto(
                    r.j.JournalId, r.j.EntryDate, r.j.SourceType.ToString(),
                    (r.j.PaymentMethod ?? PaymentMethod.SYSTEM).ToString(),
                    r.j.Description, r.l.Debit, r.l.Credit, running));
            }

            result.Add(new MemberAccountLedgerDto(
                acct.AccountId, acct.AccountNumber,
                fromDate, toDate, opening, running, totD, totC, lines));
        }

        return result;
    }
    public async Task<MemberAccountLedgerDto> GetMemberAccountLedgerDetailsAsync(long journalId)
    {
        // Get the journal entry first
        var journalEntry = await _db.JournalEntries
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(j => j.JournalId == journalId);

        if (journalEntry == null)
            throw new DomainException($"Journal entry with ID {journalId} not found");

        // Get all journal lines for this journal entry with MemberAccountId
        var journalLines = await _db.JournalLines
            .IgnoreQueryFilters()
            .Where(l => l.JournalId == journalId && l.MemberAccountId.HasValue)
            .ToListAsync();

        if (!journalLines.Any())
            throw new DomainException($"No member account lines found for journal ID {journalId}");

        // Get all unique member account IDs from journal lines
        var memberAccountIds = journalLines
            .Select(l => l.MemberAccountId.Value)
            .Distinct()
            .ToList();

        // Get account details for these member IDs
        var accounts = await _db.Accounts
            .IgnoreQueryFilters()
            .Where(a => memberAccountIds.Contains(a.AccountId))
            .ToDictionaryAsync(a => a.AccountId);

        // Get all journal lines and entries for running balance calculation
        var allJl = _db.JournalLines.IgnoreQueryFilters();
        var allJe = _db.JournalEntries.IgnoreQueryFilters();

        // Get the first account (assuming one account per journal)
        var accountId = memberAccountIds.FirstOrDefault();
        if (accountId == 0 || !accounts.TryGetValue(accountId, out var account))
            throw new DomainException($"Account not found for journal ID {journalId}");

        // Get all transactions for this account up to the journal entry date
        var allTransactions = await (
            from l in allJl
            join j in allJe on l.JournalId equals j.JournalId
            where j.TenantId == journalEntry.TenantId
                && l.MemberAccountId == accountId
                && j.EntryDate <= journalEntry.EntryDate
            orderby j.EntryDate, l.LineId
            select new
            {
                j.JournalId,
                j.EntryDate,
                j.SourceType,
                j.PaymentMethod,
                j.Description,
                l.Debit,
                l.Credit,
                l.LineId
            }
        ).ToListAsync();

        // Calculate opening balance (before this journal entry)
        var openingBalance = allTransactions
            .Where(t => t.EntryDate < journalEntry.EntryDate)
            .Sum(t => t.Credit - t.Debit);
        openingBalance = Math.Round(openingBalance, 2);

        // Process transactions and calculate running balance
        decimal runningBalance = openingBalance;
        decimal totalDebit = 0m;
        decimal totalCredit = 0m;
        var lines = new List<MemberAccountLedgerLineDto>();

        foreach (var transaction in allTransactions)
        {
            runningBalance += transaction.Credit - transaction.Debit;
            runningBalance = Math.Round(runningBalance, 2);

            // Only include lines from the requested journal
            if (transaction.JournalId == journalId)
            {
                totalDebit += transaction.Debit;
                totalCredit += transaction.Credit;

                lines.Add(new MemberAccountLedgerLineDto(
                    transaction.JournalId,
                    transaction.EntryDate,
                    transaction.SourceType.ToString(),
                    (transaction.PaymentMethod ?? PaymentMethod.SYSTEM).ToString(),
                    transaction.Description,
                    transaction.Debit,
                    transaction.Credit,
                    runningBalance
                ));
            }
        }

        return new MemberAccountLedgerDto(
            account.AccountId,
            account.AccountNumber,
            null, // fromDate not applicable
            null, // toDate not applicable
            openingBalance,
            runningBalance,
            Math.Round(totalDebit, 2),
            Math.Round(totalCredit, 2),
            lines
        );
    }

    // ===========================================================
    // Trial balance / balance sheet / income statement
    // ===========================================================

    public async Task<TrialBalanceDto> GetTrialBalanceAsync(long tenantId, DateOnly asOf)
    {
        var rows = await BuildClassTotalsAsync(tenantId, fromOpt: null, toOpt: asOf);
        var trialLines = new List<TrialBalanceLineDto>();
        decimal totD = 0m, totC = 0m;
        foreach (var r in rows)
        {
            bool isDebitNormal = r.cls is GlAccountClass.ASSET or GlAccountClass.EXPENSE;
            decimal signedBalance = isDebitNormal ? r.totalDr - r.totalCr : r.totalCr - r.totalDr;
            decimal debit = 0m, credit = 0m;
            if (signedBalance >= 0)
            {
                if (isDebitNormal) debit = signedBalance; else credit = signedBalance;
            }
            else
            {
                if (isDebitNormal) credit = -signedBalance; else debit = -signedBalance;
            }
            trialLines.Add(new TrialBalanceLineDto(r.code, r.name, r.cls.ToString(), debit, credit));
            totD += debit; totC += credit;
        }

        // Member subsidiary ledger rolls up as a single LIABILITY-class control account
        // (per design: 2000 Member Contributions Payable is the aggregate of member_account_balance).
        // Sum every journal_line.debit/credit where member_account_id is set, for this tenant.
        var memberQ =
            from l in _db.JournalLines.IgnoreQueryFilters()
            join j in _db.JournalEntries.IgnoreQueryFilters() on l.JournalId equals j.JournalId
            where j.TenantId == tenantId
                  && l.MemberAccountId != null
                  && j.EntryDate <= asOf
            select new { l.Debit, l.Credit };
        var memberAgg = await memberQ.ToListAsync();
        var memDr = memberAgg.Sum(x => x.Debit);
        var memCr = memberAgg.Sum(x => x.Credit);
        var memSigned = memCr - memDr;   // positive = corpus liability owed to members
        if (memDr != 0 || memCr != 0)
        {
            var memDebit = memSigned < 0 ? -memSigned : 0m;
            var memCredit = memSigned > 0 ? memSigned : 0m;
            trialLines.Add(new TrialBalanceLineDto(
                SystemGl.MemberContributionsPayable,
                "Member Contributions Payable (subsidiary rollup)",
                GlAccountClass.LIABILITY.ToString(),
                Math.Round(memDebit, 2), Math.Round(memCredit, 2)));
            totD += Math.Round(memDebit, 2);
            totC += Math.Round(memCredit, 2);
        }

        return new TrialBalanceDto(asOf, Math.Round(totD, 2), Math.Round(totC, 2),
            Math.Round(totD, 2) == Math.Round(totC, 2), trialLines);
    }

    public async Task<BalanceSheetDto> GetBalanceSheetAsync(long tenantId, DateOnly asOf)
    {
        var rows = await BuildClassTotalsAsync(tenantId, fromOpt: null, toOpt: asOf);
        BalanceSheetGroupDto Group(GlAccountClass cls)
        {
            var lines = rows.Where(r => r.cls == cls)
                .Select(r =>
                {
                    bool dn = cls is GlAccountClass.ASSET or GlAccountClass.EXPENSE;
                    decimal bal = dn ? r.totalDr - r.totalCr : r.totalCr - r.totalDr;
                    return new TrialBalanceLineDto(r.code, r.name, cls.ToString(),
                        dn ? Math.Max(bal, 0) : (bal < 0 ? -bal : 0),
                        dn ? (bal < 0 ? -bal : 0) : Math.Max(bal, 0));
                }).ToList();
            decimal total = lines.Sum(l => cls is GlAccountClass.ASSET or GlAccountClass.EXPENSE
                ? l.Debit - l.Credit : l.Credit - l.Debit);
            return new BalanceSheetGroupDto(cls.ToString(), Math.Round(total, 2), lines);
        }
        var assets = Group(GlAccountClass.ASSET);
        var liabilities = Group(GlAccountClass.LIABILITY);
        var equity = Group(GlAccountClass.EQUITY);

        // Add member subsidiary rollup to liabilities (corpus owed to members)
        var memQ =
            from l in _db.JournalLines.IgnoreQueryFilters()
            join j in _db.JournalEntries.IgnoreQueryFilters() on l.JournalId equals j.JournalId
            where j.TenantId == tenantId
                  && l.MemberAccountId != null
                  && j.EntryDate <= asOf
            select new { l.Debit, l.Credit };
        var memAgg = await memQ.ToListAsync();
        decimal memNetCr = memAgg.Sum(x => x.Credit) - memAgg.Sum(x => x.Debit);
        if (memNetCr != 0)
        {
            var memLine = new TrialBalanceLineDto(
                SystemGl.MemberContributionsPayable,
                "Member Contributions Payable (subsidiary rollup)",
                GlAccountClass.LIABILITY.ToString(),
                memNetCr < 0 ? Math.Round(-memNetCr, 2) : 0m,
                memNetCr > 0 ? Math.Round(memNetCr, 2) : 0m);
            liabilities = new BalanceSheetGroupDto(
                liabilities.AccountClass,
                Math.Round(liabilities.Total + Math.Max(memNetCr, 0), 2),
                liabilities.Lines.Append(memLine).ToList());
        }

        // Retained earnings = sum(income) - sum(expense), inception-to-date.
        decimal income = rows.Where(r => r.cls == GlAccountClass.INCOME).Sum(r => r.totalCr - r.totalDr);
        decimal expense = rows.Where(r => r.cls == GlAccountClass.EXPENSE).Sum(r => r.totalDr - r.totalCr);
        decimal retained = Math.Round(income - expense, 2);

        bool balanced = Math.Round(assets.Total, 2) == Math.Round(liabilities.Total + equity.Total + retained, 2);
        return new BalanceSheetDto(asOf, assets, liabilities, equity, retained, balanced);
    }

    public async Task<IncomeStatementDto> GetIncomeStatementAsync(long tenantId, DateOnly from, DateOnly to)
    {
        var rows = await BuildClassTotalsAsync(tenantId, from, to);
        BalanceSheetGroupDto Group(GlAccountClass cls)
        {
            var lines = rows.Where(r => r.cls == cls)
                .Select(r =>
                {
                    bool dn = cls is GlAccountClass.ASSET or GlAccountClass.EXPENSE;
                    decimal bal = dn ? r.totalDr - r.totalCr : r.totalCr - r.totalDr;
                    return new TrialBalanceLineDto(r.code, r.name, cls.ToString(),
                        dn ? Math.Max(bal, 0) : (bal < 0 ? -bal : 0),
                        dn ? (bal < 0 ? -bal : 0) : Math.Max(bal, 0));
                }).ToList();
            decimal total = lines.Sum(l => cls is GlAccountClass.ASSET or GlAccountClass.EXPENSE
                ? l.Debit - l.Credit : l.Credit - l.Debit);
            return new BalanceSheetGroupDto(cls.ToString(), Math.Round(total, 2), lines);
        }
        var inc = Group(GlAccountClass.INCOME);
        var exp = Group(GlAccountClass.EXPENSE);
        return new IncomeStatementDto(from, to, inc, exp, Math.Round(inc.Total - exp.Total, 2));
    }

    // ===========================================================
    // Internal: per-GL class totals (Dr/Cr sums) over a date window
    // ===========================================================
    private async Task<List<(string code, string name, GlAccountClass cls, decimal totalDr, decimal totalCr)>>
        BuildClassTotalsAsync(long tenantId, DateOnly? fromOpt, DateOnly? toOpt)
    {
        var q =
            from g in _db.GlAccounts.IgnoreQueryFilters().Where(g => g.TenantId == tenantId)
            join l in _db.JournalLines.IgnoreQueryFilters() on g.GlAccountId equals l.GlAccountId into lj
            from l in lj.DefaultIfEmpty()
            join j in _db.JournalEntries.IgnoreQueryFilters() on (l == null ? (long?)null : l.JournalId) equals (long?)j.JournalId into jj
            from j in jj.DefaultIfEmpty()
            select new { g, l, j };

        var rows = await q.ToListAsync();
        var grouped = rows
            .Where(x => x.l == null || x.j == null
                        || ((!fromOpt.HasValue || x.j.EntryDate >= fromOpt.Value)
                            && (!toOpt.HasValue || x.j.EntryDate <= toOpt.Value)))
            .GroupBy(x => new { x.g.GlAccountId, x.g.Code, x.g.Name, x.g.AccountClass })
            .Select(grp => (
                code: grp.Key.Code,
                name: grp.Key.Name,
                cls: grp.Key.AccountClass,
                totalDr: Math.Round(grp.Sum(x => x.l == null ? 0m : x.l.Debit), 2),
                totalCr: Math.Round(grp.Sum(x => x.l == null ? 0m : x.l.Credit), 2)
            ))
            .OrderBy(x => x.code)
            .ToList();
        return grouped;
    }

    public async Task<IEnumerable<PaymentReportDto>> GetPaymentReportAsync(
        long? tenantId = null,
        long? branchId = null,
        string? type = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null)
    {
        // Check permissions
        var isAdmin = _tenantCtx.Role == "SIFIN_ADMIN" ||
                      _tenantCtx.Role == "SUPER_ADMIN";

        // If tenantId is null (user wants all data), check if they're admin
        if (!tenantId.HasValue && !isAdmin)
        {
            tenantId = _tenantCtx.TenantId;
        }

        if (tenantId.HasValue && !isAdmin && tenantId.Value != _tenantCtx.TenantId)
        {
            throw new UnauthorizedAccessException($"You don't have permission to access TenantId: {tenantId.Value}");
        }

        // ✅ Start with query - use proper joins
        var query = from je in _db.JournalEntries.IgnoreQueryFilters()
                    join jl in _db.JournalLines.IgnoreQueryFilters()
                        on je.JournalId equals jl.JournalId
                    select new { je, jl };

        // Apply tenant filter if provided
        if (tenantId.HasValue)
            query = query.Where(x => x.je.TenantId == tenantId.Value);

        // ✅ Apply branch filter on JournalEntry (not DTO)
        //if (branchId.HasValue)
        //    query = query.Where(x => x.je.BranchId == branchId.Value);

        // Apply source type filter if provided
        if (!string.IsNullOrEmpty(type))
        {
            if (Enum.TryParse<JournalSourceType>(type, true, out var sourceType))
            {
                query = query.Where(x => x.je.SourceType == sourceType);
            }
            else
            {
                var validValues = string.Join(", ", Enum.GetNames(typeof(JournalSourceType)));
                throw new DomainException($"Invalid SourceType: '{type}'. Valid values: {validValues}");
            }
        }

        // Apply date filters if provided
        if (fromDate.HasValue)
            query = query.Where(x => x.je.EntryDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(x => x.je.EntryDate <= toDate.Value);

        // ✅ Project to DTO after all filters
        return await query.Select(x => new PaymentReportDto
        {
            JournalId = x.je.JournalId,
            TenantId = x.je.TenantId,
            //BranchId = x.je.BranchId,        // ✅ Include BranchId
            EntryDate = x.je.EntryDate,
            SourceType = x.je.SourceType.ToString(),
            PaymentMethod = x.je.PaymentMethod.ToString(),
            Description = x.je.Description,
            Debit = x.jl.Debit,
            Credit = x.jl.Credit,
            Amount = x.jl.RunningBalance
            status
        }).ToListAsync();
    }


    public async Task<IEnumerable<KycReportDto>> GetKycReportAsync(
    long? tenantId = null,
    long? branchId = null,
    string? type = null,
    DateOnly? fromDate = null,
    DateOnly? toDate = null)
    {
        // Start with base query - bypass global tenant filters
        var query = _db.Members.IgnoreQueryFilters().AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(m => m.TenantId == tenantId.Value);

        // Apply branch filter if provided (works for both scenarios)
        if (branchId.HasValue)
            query = query.Where(m => m.BranchId == branchId.Value);

        // Apply member type filter if provided
        if (!string.IsNullOrEmpty(type))
        {
            if (Enum.TryParse<MemberType>(type, true, out var memberType))
            {
                query = query.Where(m => m.MemberType == memberType);
            }
            else
            {
                var validValues = string.Join(", ", Enum.GetNames(typeof(MemberType)));
                throw new DomainException($"Invalid MemberType: '{type}'. Valid values: {validValues}");
            }
        }

        // Apply date filters if provided
        if (fromDate.HasValue)
            query = query.Where(m => m.KycApprovedAt >= fromDate.Value.ToDateTime(TimeOnly.MinValue));

        if (toDate.HasValue)
            query = query.Where(m => m.KycApprovedAt <= toDate.Value.ToDateTime(TimeOnly.MaxValue));

        // Project to DTO
        return await query.Select(m => new KycReportDto
        {
            MemberId = m.MemberId,
            TenantId = m.TenantId,
            BranchId = m.BranchId,
            MemberType = m.MemberType.ToString(),
            KycStatus = m.KycStatus.ToString(),
            KycTier = m.KycTier.ToString(),
            KycApprovedAt = m.KycApprovedAt,
            CustomerIdentifierCode = m.CustomerIdentifierCode,
            FullName = m.FullName,
            Phone = m.Phone,
            Email = m.Email,
            PanNumber = m.PanNumber,
            IdType = m.IdType,
            IdNumber = m.IdNumber
        }).ToListAsync();
    }



    public async Task<IEnumerable<AccountOpenReportDto>> GetAccountOpenReportAsync(
        long? tenantId = null,
        long? branchId = null,
         string? type = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null)
    {
        // Check permissions
        var isAdmin = _tenantCtx.Role == "SIFIN_ADMIN" ||
                      _tenantCtx.Role == "SUPER_ADMIN";

        // If tenantId is null (user wants all data), check if they're admin
        if (!tenantId.HasValue && !isAdmin)
        {
            // Non-admin trying to access all tenants - restrict to their own tenant
            tenantId = _tenantCtx.TenantId;
        }

        // If tenantId is provided, check if user has access
        if (tenantId.HasValue && !isAdmin && tenantId.Value != _tenantCtx.TenantId)
        {
            throw new UnauthorizedAccessException($"You don't have permission to access TenantId: {tenantId.Value}");
        }

        var query = _db.Accounts.IgnoreQueryFilters().AsQueryable();

        // Apply tenant filter if provided
        if (tenantId.HasValue)
            query = query.Where(a => a.TenantId == tenantId.Value);

        // Apply branch filter if provided
        if (branchId.HasValue)
            query = query.Where(a => a.BranchId == branchId.Value);

        // ✅ Apply account type filter if provided
        //if (type.HasValue)
        //    query = query.Where(a => a.AccountType == type.Value);

        // Apply date filters if provided
        if (fromDate.HasValue)
            query = query.Where(a => a.AccountOpenDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(a => a.AccountOpenDate <= toDate.Value);

        // Project to DTO
        return await query.Select(a => new AccountOpenReportDto
        {
            AccountId = a.AccountId,
            TenantId = a.TenantId,
            BranchId = a.BranchId,
            AccountNumber = a.AccountNumber,
            AccountOpenDate = a.AccountOpenDate,
            Status = a.Status.ToString(),
            //AccountType = a.AccountType.ToString(),
            FullName = a.CustomerName,
            Phone = a.PhoneNo,
            //Email = a.Email,
            Balance = a.MonthlyContribution
        }).ToListAsync();
    }

}
