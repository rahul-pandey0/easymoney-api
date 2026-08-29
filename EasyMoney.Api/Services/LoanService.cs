using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace EasyMoney.Api.Services;

public interface ILoanService
{
    /// <summary>Records the prize disbursement during cycle resolution.</summary>
    Task<Loan> DisburseAsync(long accountId, long cycleId, decimal principal);

    Task<Loan?> GetByAccountAsync(long accountId);
    Task<List<Loan>> GetByAccountbytenant();
    Task<Loan?> GetByAccountsdata(int loanId);  
    Task<List<BidderWithoutLoanDto>> GetByAccountData();  
    Task<LoanDto> CreateLoanAsync(CreateLoanDto loan);
    //Task<Loan>
    Task<Loan> ApproveLoanAsync(long loanId);

    Task<Loan> GetLoanAsync(long loanId);
    Task<Loan> CalculateNetDisbursementAsync(long loanId);
    //Task<Loan> DisburseLoanAsync(long loanId);
    Task<IEnumerable<Loan>> GetPendingApprovalLoansAsync();
    Task<IEnumerable<Loan>> GetApprovedPendingDisbursementLoansAsync();
    Task<DisbursementVoucherDto> DisburseLoanAsync(long loanId, DisbursementRequestDto request);
    Task<LoanDto> UpdateLoanAsync(int id, CreateLoanDto updateLoanDto); // Add this

}

public class LoanService : ILoanService
{
    private readonly EasyMoneyDbContext _db;
    private readonly IAccountingService _accounting;
    private readonly ITenantContext _ctx;
    private readonly ILogger<LoanService> _log;

    public LoanService(EasyMoneyDbContext db, IAccountingService accounting, ITenantContext ctx, ILogger<LoanService> log)
    {
        _db = db; _accounting = accounting; _ctx = ctx; _log = log;

    }

    public async Task<Loan> DisburseAsync(long accountId, long cycleId, decimal principal)
    {
        if (principal <= 0) throw new DomainException("Principal must be > 0"); 
        var a = await _db.Accounts.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.AccountId == accountId)
            ?? throw new DomainException($"Account {accountId} not found");
        //if (await _db.Loans.IgnoreQueryFilters().AnyAsync(l => l.AccountId == accountId))
        //    throw new DomainException("Account already has a loan");

        var cycle = await _db.BiddingCycles.IgnoreQueryFilters().FirstAsync(c => c.CycleId == cycleId);

        var loan = new Loan
        {
            TenantId = a.TenantId,
            AccountId = a.AccountId,
            CycleId = cycleId,
            PrincipalAmount = principal,
            DisbursedAt = DateTime.UtcNow,
            AuthorizedBy = _ctx.UserId,
            AuthorizedAt = DateTime.UtcNow
        };
        _db.Loans.Add(loan);

        // Subsidiary ledger entry
        _db.LedgerEntries.Add(new LedgerEntry
        {
            TenantId = a.TenantId,
            AccountId = a.AccountId,
            CycleId = cycleId,
            EntryType = LedgerEntryType.LOAN_DISBURSEMENT,
            Amount = principal,
            EntryDate = cycle.CycleMonth,
            Description = $"Loan disbursed (cycle {cycle.CycleMonth:yyyy-MM})",
            CreatedBy = _ctx.UserId
        });
        await _db.SaveChangesAsync();

        //var data = await _db.SchemeConfigs.IgnoreQueryFilters().FirstAsync(s => s.TenantId == _ctx.TenantId);
        //var orgfee = await _db.GeneralLedgerMaster.IgnoreQueryFilters().FirstAsync(s => s.GlId == data.OrgFeeGlId);
        //var bankcode = await _db.GeneralLedgerMaster.IgnoreQueryFilters().FirstAsync(s => s.Code == data.Reserve1);
        // GL journal: Dr Loans Receivable (asset booked), Cr Bank (cash paid out).
        await _accounting.PostJournalAsync(
            a.TenantId, cycle.CycleMonth, JournalSourceType.LOAN_DISBURSEMENT,
            sourceId: loan.LoanId, paymentMethod: PaymentMethod.BANK_TRANSFER,
            description: $"Loan disbursement to {a.AccountNumber} (cycle {cycle.CycleMonth:yyyy-MM})",
            lines: new[]
            {
                new JournalLineInput(EntryTarget.GL,SystemGl.CashInHand, null, principal, 0),
                new JournalLineInput(EntryTarget.GL, SystemGl.Bank, null, 0, principal)
                //    new JournalLineInput(EntryTarget.1001, null, principal, 0),
                //new JournalLineInput(EntryTarget.GL, 1002e, null, 0, principal)
            },
            createdBy: _ctx.UserId, authorizedBy: _ctx.UserId);

        _log.LogInformation("Disbursed loan {Lid} ₹{P} to account {Aid}", loan.LoanId, principal, accountId);
        return loan;
    }

    public async Task<LoanDto> CreateLoanAsync(CreateLoanDto createLoanDto)
    {

        try
        {
            // Validate cycle
            //var cycle = await _db.BiddingCycles.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.CycleId == createLoanDto.CycleId);
            var cycle = await _db.BiddingCycles.IgnoreQueryFilters().FirstAsync(s => s.CycleId == createLoanDto.CycleId);

            if (cycle == null)
                throw new DomainException($"Cycle with ID {createLoanDto.CycleId} not found");

            //if (cycle.Status == "CLOSED" || cycle.Status == "RESOLVED")
            //    throw new InvalidOperationException("Cannot create loan for closed or resolved cycle");

            if (createLoanDto.AccountId <= 0)
                throw new ArgumentException("Invalid AccountId");

            // Create Loan
            var loan = new Loan
            {
                TenantId = _ctx.TenantId.Value,
                AccountId = createLoanDto.AccountId,
                CycleId = createLoanDto.CycleId,
                PrincipalAmount = createLoanDto.PrincipalAmount,
                DisbursedAt = createLoanDto.DisbursedAt,
                OutstandingBalance = createLoanDto.PrincipalAmount,
                Status = "ACTIVE",
                PhoneNumber = createLoanDto.PhoneNumber,
                CustomerName = createLoanDto.CustomerName,
                BidDate = createLoanDto.BidDate,
                BranchId = createLoanDto.BranchId ?? _ctx.BranchId,
                OrgFeeAmount = createLoanDto.OrgFeeAmount,
                SifinCommission = createLoanDto.SifinCommission,
                NetDisbursementAmount = createLoanDto.NetDisbursementAmount,
                ProcessingFee = createLoanDto.ProcessingFee,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _ctx.UserId,
                AuthStatus = false,


                LoanRemark = createLoanDto.LoanRemark ?? "LOAN Creation",
                LoanApplicationStatus = createLoanDto.LoanApplicationStatus,
                SecurityDocStatus = createLoanDto.SecurityDocStatus,
                SecurityDocRemarks = createLoanDto.SecurityDocRemarks,
                ChequeObtained = createLoanDto.ChequeObtained,
                ChequeAccountNo = createLoanDto.ChequeAccountNo,
                ChequeBankName = createLoanDto.ChequeBankName,
                ChequeNo = createLoanDto.ChequeNo,
                ChequeDate = createLoanDto.ChequeDate,
                LoanReleaseStatus =createLoanDto.LoanReleaseStatus,
                Remarks = createLoanDto.Remarks,

            };

            _db.Loans.Add(loan);
            await _db.SaveChangesAsync();

            // Create Co-Borrowers if any
            if (createLoanDto.CoBorrowers != null && createLoanDto.CoBorrowers.Any())
            {
                var coBorrowers = createLoanDto.CoBorrowers.Select(cb => new CoBorrower
                {
                    TenantId = _ctx.TenantId.Value,
                    LoanId = loan.LoanId,
                    //BranchId = loan.BranchId,
                    CoBorrowerAccountId=cb.CoBorrowerAccountId,
                    CoBorrowerName = cb.CoBorrowerName,
                    CoBorrowerPhone = cb.CoBorrowerPhone,
                    CoBorrowerEmail = cb.CoBorrowerEmail,
                    CoBorrowerAddress = cb.CoBorrowerAddress,
                    CoBorrowerAccountNumber = cb.CoBorrowerAccountNumber,
                    CoopName = cb.CoopName,
                    CoopMobileNumber = cb.CoopMobileNumber,
                    CoopAccountId = cb.CoopAccountId,
                    CoopAccountNumber = cb.CoopAccountNumber,
                    CoBorrowerRemarks = cb.CoBorrowerRemarks,
                    IsPrimaryCoBorrower = cb.IsPrimaryCoBorrower,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = _ctx.UserId,
                    IsActive = true
                }).ToList();

                _db.CoBorrowers.AddRange(coBorrowers);
                await _db.SaveChangesAsync();
            }

            // Create Approval Request
            var approval = new ApprovalRequest
            {
                TenantId = _ctx.TenantId.Value,
                ActionType = ApprovalActionType.CREATE_LOAN,
                EntityType = "Loan Create",
                EntityId = loan.LoanId,
                Payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    loan.LoanId,
                    loan.CycleId,
                    loan.AccountId,
                    loan.AuthStatus,
                    loan.TenantId,
                    loan.CreatedBy,
                    loan.PrincipalAmount,
                    loan.DisbursedAt
                }),
                Status = ApprovalStatus.PENDING,
                RequestedBy = _ctx.UserId.Value,
                RequestedAt = DateTime.UtcNow
            };

            _db.ApprovalRequests.Add(approval);
            await _db.SaveChangesAsync();
            //await transaction.CommitAsync();

            _log.LogInformation("Disbursed loan {LoanId} ₹{PrincipalAmount} to account {AccountId}",
                loan.LoanId, loan.PrincipalAmount, loan.AccountId);

            // Return the created loan with co-borrowers
            return await GetLoanByIdAsync(loan.LoanId);
        }
        catch (Exception ex)
        {
            //await transaction.RollbackAsync();
            _log.LogError(ex, "Error creating loan for account {AccountId}", createLoanDto.AccountId);
            throw;
        }
    }
    public async Task<LoanDto> GetLoanByIdAsync(long loanId)
    {
        var loan = await _db.Loans
            .Include(l => l.CoBorrowerDetails)
            .FirstOrDefaultAsync(l => l.LoanId == loanId && l.TenantId == _ctx.TenantId);

        if (loan == null)
            throw new DomainException($"Loan with ID {loanId} not found");

        return MapToLoanDto(loan);
    }
    private LoanDto MapToLoanDto(Loan loan)
    {
        return new LoanDto(
            loan.LoanId,
            loan.AccountId,
            loan.CycleId,
            loan.PrincipalAmount,
            loan.DisbursedAt,
            loan.Status,
            loan.AuthStatus,
            loan.AuthorizedAt,
            loan.AuthorizedBy,
            loan.NetDisbursementAmount,
            loan.ProcessingFee,
            loan.OrgFeeAmount,
            loan.SifinCommission,
            loan.OutstandingBalance,
            loan.LoanRemark,
            loan.CustomerName,
            loan.PhoneNumber,
            loan.BranchId,
            loan.CoBorrowerDetails?.Where(cb => cb.IsActive).Select(cb => new CoBorrowerDto(
                cb.CoBorrowerId,
                cb.LoanId,
                cb.CoBorrowerName,
                cb.CoBorrowerAccountId,
                cb.CoBorrowerPhone,
                cb.CoBorrowerEmail,
                cb.CoBorrowerAddress,
                cb.CoBorrowerAccountNumber,
                cb.CoopName,
                cb.CoopMobileNumber,
                cb.CoopAccountId,
                cb.CoopAccountNumber,
                cb.CoBorrowerRemarks,
                cb.IsPrimaryCoBorrower,
                cb.IsActive
            )).ToList() ?? new List<CoBorrowerDto>()
        );
    }
    public async Task<IEnumerable<LoanDto>> GetLoansByTenantAsync()
    {
        var loans = await _db.Loans
            .Include(l => l.CoBorrowerDetails)
            .Where(l => l.TenantId == _ctx.TenantId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        return loans.Select(MapToLoanDto);
    }
    /* public async Task<Loan> CreateLoanAsync(Loan loan)
     {
         try
         {
             var cycle = await _db.BiddingCycles.IgnoreQueryFilters().FirstAsync(s => s.CycleId == loan.CycleId);
             //if (cycle.Status == OPEN)
             //{throw new Exception("plaes")
             //}

             if (loan.AccountId <= 0)
                 throw new ArgumentException("Invalid AccountId");

             // Set default values
             loan.OutstandingBalance = loan.PrincipalAmount;
             loan.Status = "ACTIVE";
             loan.CreatedAt = DateTime.UtcNow;
             loan.TenantId=_ctx.TenantId.Value;
             loan.BranchId = _ctx.BranchId;
             loan.LoanRemark = "LOAN Craetion";
             // Add to database
             _db.Loans.Add(loan);
             await _db.SaveChangesAsync();
             var approval = new ApprovalRequest
             {
                 TenantId = _ctx.TenantId,
                 ActionType = ApprovalActionType.CREATE_LOAN,
                 EntityType = "Loan Create",
                 EntityId = loan.LoanId,
                 Payload = System.Text.Json.JsonSerializer.Serialize(new
                 {
                     loan.LoanId,
                     loan.CycleId,
                     loan.AccountId,
                     loan.AuthStatus,
                     loan.TenantId,
                     loan.CreatedBy,
                 }),
                 Status = ApprovalStatus.PENDING,
                 RequestedBy = _ctx.UserId.Value,
                 RequestedAt = DateTime.UtcNow
             };

             _db.ApprovalRequests.Add(approval);
             await _db.SaveChangesAsync();


             _log.LogInformation("Disbursed loan {LoanId} ₹{PrincipalAmount} to account {AccountId}",
                 loan.LoanId, loan.PrincipalAmount, loan.AccountId);

             return loan;
         }
         catch (Exception ex)
         {
             _log.LogError(ex, "Error creating loan for account {AccountId}", loan.AccountId);
             throw;
         }
     }

     */
    public Task<Loan?> GetByAccountAsync(long accountId) =>
        _db.Loans.IgnoreQueryFilters().FirstOrDefaultAsync(l => l.AccountId == accountId);

    public Task<List<Loan>> GetByAccountbytenant() =>
        _db.Loans.IgnoreQueryFilters()
            .Where(l => l.TenantId == _ctx.TenantId)
            .ToListAsync();


    public async Task<Loan?> GetByAccountsdata(int loanId)
    {
        try 
        {
            return await _db.Loans
                .IgnoreQueryFilters()
                .Include(l => l.CoBorrowerDetails)
                .FirstOrDefaultAsync(l =>
                    l.TenantId == _ctx.TenantId &&
                    l.LoanId == loanId);
        }
        catch (Exception)
        {
            throw;
        }
    }
    
    //public Task<List<Loan>> GetByAccountData() =>
    //    _db.Loans.IgnoreQueryFilters()
    //        .Where(l => l.TenantId == _ctx.TenantId)
    //        .ToListAsync();


    // Most efficient approach using NOT EXISTS   
    // Most efficient approach using NOT EXISTS 
    public async Task<List<Account>> GetByAccountData1()
    {
        try
        {
            var tenantId = _ctx.TenantId.Value;

            var query = _db.Accounts
                .IgnoreQueryFilters()
                .Where(a => a.TenantId == tenantId)
                .Where(a => !_db.Loans
                    .IgnoreQueryFilters()
                    .Any(l => l.TenantId == tenantId && l.AccountId == a.AccountId))
                .OrderBy(a => a.AccountId);

            return await query.ToListAsync();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error getting accounts without loans using NOT EXISTS");
            throw;
        }
    }


    public async Task<List<BidderWithoutLoanDto>> GetByAccountData()
    {
        var tenantId = _ctx.TenantId ?? 0;

        // Get current cycle
        var cycle = await _db.BiddingCycles
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId
                       && (c.Status == CycleStatus.OPEN
                           || c.Status == CycleStatus.CLOSED
                           || c.Status == CycleStatus.RESOLVED
                           || c.Status == CycleStatus.NO_BID))
            .OrderByDescending(c => c.CycleMonth)
            .FirstOrDefaultAsync();

        if (cycle == null) return new List<BidderWithoutLoanDto>();

        // Get account IDs that have loans
        var accountIdsWithLoans = await _db.Loans
            .IgnoreQueryFilters()
            .Where(l => l.TenantId == tenantId)
            .Select(l => l.AccountId)
            .Distinct()
            .ToListAsync();

        // Get all bids for accounts WITHOUT loans using LINQ query with anti-join
        var query = from bid in _db.Bids
                    join account in _db.Accounts on bid.AccountId equals account.AccountId
                    join member in _db.Members on account.MemberId equals member.MemberId
                    where bid.CycleId == cycle.CycleId
                          && !accountIdsWithLoans.Contains(account.AccountId) // Anti-join condition
                    select new BidderWithoutLoanDto
                    {
                        BidId = bid.BidId,
                        AccountId = bid.AccountId,
                        AccountNumber = account.AccountNumber,
                        MemberId = member.MemberId,
                        MemberName = member.FullName,
                        MemberPhone = member.Phone,
                        Email = member.Email,
                        BidPct = bid.BidPct,
                        MonthlyContribution = account.MonthlyContribution,
                        SubmittedAt = bid.SubmittedAt,
                        IsApproved = bid.IsApproved,
                        IsWinner = bid.IsWinner,
                        Status = bid.IsWinner ? "Winner" : (bid.IsApproved ? "Approved" : "Pending"),
                        StatusBadge = bid.IsWinner ? "winner" : (bid.IsApproved ? "approved" : "pending"),
                        CycleId = cycle.CycleId,
                        CycleMonth = cycle.CycleMonth
                    };

        var result = await query.ToListAsync();

        // Calculate additional fields (forfeiture, prize) in memory
        var scheme = await _db.SchemeConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId);

        var participants = await _db.Accounts
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId
                        && (a.Status == AccountStatus.ACTIVE || a.Status == AccountStatus.PRIZED)
                        && a.AccountOpenDate <= cycle.CycleMonth
                        && a.TenureEndDate > cycle.CycleMonth)
            .ToListAsync();

        var grossCorpus = participants.Sum(a => a.MonthlyContribution);
        var orgFeeAmount = Math.Round(grossCorpus * (scheme?.OrgFeePct ?? 0) / 100m, 2);
        var bidPool = grossCorpus - orgFeeAmount;

        foreach (var item in result)
        {
            var forfeiture = Math.Round(grossCorpus * item.BidPct / 100m, 2);
            var prizeIfWins = bidPool - forfeiture;
            item.ForfeitureAmount = forfeiture;
            item.PrizeIfWins = prizeIfWins < 0 ? 0 : prizeIfWins;
        }

        return result.OrderByDescending(r => r.BidPct).ToList();
    }


    //public async Task<Loan> ApproveLoanAsync(long loanId)
    //{
    //    var data = await _db.Loans.IgnoreQueryFilters()
    //        .FirstOrDefaultAsync(x => x.LoanId == loanId);

    //    if (data is null)
    //        throw new DomainException($"Loan {loanId} not found.");

    //            // Approve this specific bid
    //    data.AuthStatus = true;
    //    data.AuthorizedAt = DateTime.UtcNow;
    //    data.AuthorizedBy = _ctx.UserId;
    //    data.BranchId = _ctx.BranchId;

    //    await _db.SaveChangesAsync();

    //    return data;
    //}

    //public async Task<Loan> GetLoanAsync(long loanId)
    //{
    //    return await _db.Loans.IgnoreQueryFilters()
    //        .FirstOrDefaultAsync(l => l.LoanId == loanId)
    //        ?? throw new DomainException($"Loan {loanId} not found");
    //}
    public async Task<Loan> ApproveLoanAsync(long loanId)
    {
        var loan = await _db.Loans.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.LoanId == loanId)
            ?? throw new DomainException($"Loan {loanId} not found.");



        // Approve the loan
        loan.AuthStatus = true;
        loan.AuthorizedAt = DateTime.UtcNow;
        loan.AuthorizedBy = _ctx.UserId;
        loan.Status = "ACTIVE";
        loan.BranchId = _ctx.BranchId;

        await _db.SaveChangesAsync();

        // Update approval request status
        var approvalRequest = await _db.ApprovalRequests
            .FirstOrDefaultAsync(a => a.EntityType == "Loan" && a.EntityId == loanId && a.Status == ApprovalStatus.PENDING);
        if (approvalRequest != null)
        {
            approvalRequest.Status = ApprovalStatus.APPROVED;
            //approvalRequest.ApprovedBy = _ctx.UserId;
            //approvalRequest.ApprovedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        // Calculate net disbursement after approval
        //await CalculateNetDisbursementAsync(loanId);

        _log.LogInformation("Loan {LoanId} approved by user {UserId}", loanId, _ctx.UserId);
        return loan;
    }

    // NEW METHOD - Get Loan by ID
    public async Task<Loan> GetLoanAsync(long loanId)
    {
        return await _db.Loans.IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.LoanId == loanId)
            ?? throw new DomainException($"Loan {loanId} not found");
    }

    // NEW METHOD - Calculate net disbursement
    public async Task<Loan> CalculateNetDisbursementAsync(long loanId)
    {
        var loan = await GetLoanAsync(loanId);

        if (!loan.AuthStatus)
            throw new DomainException($"Loan {loanId} is not approved yet");

        // Get the cycle
        var cycle = await _db.BiddingCycles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.CycleId == loan.CycleId)
            ?? throw new DomainException($"Cycle {loan.CycleId} not found");

        var scheme = await _db.SchemeConfigs.IgnoreQueryFilters()
            .FirstAsync(s => s.TenantId == cycle.TenantId);

        // Calculate deductions
        var processingFeePct = scheme.OrgFeePct; // You may need to add this field
        var orgFeeAmount = Math.Round((decimal)(cycle.GrossCorpus * scheme.OrgFeePct / 100m), 2);
        var sifinShare = Math.Round(orgFeeAmount * scheme.SifinCommissionPct / 100m, 2);
        var processingFee = Math.Round(loan.PrincipalAmount * processingFeePct / 100m, 2);

        // Calculate net disbursement amount
        var netAmount = loan.PrincipalAmount - processingFee; // Subtract any fees

        // Store calculations
        //loan.ProcessingFee = processingFee;
        loan.OrgFeeAmount = orgFeeAmount;
        loan.SifinCommission = sifinShare;
        loan.NetDisbursementAmount = netAmount;

        await _db.SaveChangesAsync();

        return loan;
    }

    // NEW METHOD - Disburse Loan
    //public async Task<Loan> DisburseLoanAsync(long loanId)
    //{
    //    var loan = await GetLoanAsync(loanId);

    //    // Validate loan is approved
    //    if (loan.AuthStatus==false)
    //        throw new DomainException($"Loan {loanId} is not approved yet");

    //    // Validate loan is not already disbursed
    //    //if (loan.DisbursedAt)
    //    //    throw new DomainException($"Loan {loanId} has already been disbursed");

    //    // Calculate net amount if not already calculated
    //    if (!loan.NetDisbursementAmount.HasValue)
    //        await CalculateNetDisbursementAsync(loanId);

    //    var account = await _db.Accounts.IgnoreQueryFilters()
    //        .FirstOrDefaultAsync(a => a.AccountId == loan.AccountId)
    //        ?? throw new DomainException($"Account {loan.AccountId} not found");

    //    var cycle = await _db.BiddingCycles.IgnoreQueryFilters()
    //        .FirstOrDefaultAsync(c => c.CycleId == loan.CycleId)
    //        ?? throw new DomainException($"Cycle {loan.CycleId} not found");

    //    var netAmount = loan.NetDisbursementAmount ?? loan.PrincipalAmount;

    //    // Update loan status
    //    loan.Status = "ACTIVE";
    //    loan.DisbursedAt = DateTime.UtcNow;
    //    loan.OutstandingBalance = netAmount;
    //    //loan.DisbursedBy = _ctx.UserId;

    //    await _db.SaveChangesAsync();

    //    // Update account balance
    //    account.LoanAmount = (account.LoanAmount) + netAmount;
    //    await _db.SaveChangesAsync();

    //    // Create GL entries for disbursement
    //    await _accounting.PostJournalAsync(
    //        account.TenantId,
    //        cycle.CycleMonth,
    //        JournalSourceType.LOAN_DISBURSEMENT,
    //        sourceId: loan.LoanId,
    //        PaymentMethod.BANK_TRANSFER,
    //        description: $"Loan disbursement to {account.AccountNumber} (cycle {cycle.CycleMonth:yyyy-MM})",
    //        lines: new[]
    //        {
    //            new JournalLineInput(EntryTarget.GL, SystemGl.LoansReceivable, null, netAmount, 0),
    //            new JournalLineInput(EntryTarget.GL, SystemGl.Bank, null, 0, netAmount)
    //        },
    //        createdBy: _ctx.UserId,
    //        authorizedBy: _ctx.UserId);

    //    _db.LedgerEntries.Add(new LedgerEntry
    //    {
    //        TenantId = account.TenantId,
    //        AccountId = account.AccountId,
    //        CycleId = cycle.CycleId,
    //        EntryType = LedgerEntryType.LOAN_DISBURSEMENT,
    //        Amount = netAmount,
    //        EntryDate = cycle.CycleMonth,
    //        Description = $"Loan disbursed (cycle {cycle.CycleMonth:yyyy-MM})",
    //        CreatedBy = _ctx.UserId
    //    });

    //    await _db.SaveChangesAsync();

    //    _log.LogInformation("Disbursed loan {LoanId} ₹{Amount} to account {AccountId}",
    //        loan.LoanId, netAmount, loan.AccountId);

    //    return loan;
    //}

    // NEW METHOD - Get pending approval loans
    public async Task<IEnumerable<Loan>> GetPendingApprovalLoansAsync()
    {
        return await _db.Loans.IgnoreQueryFilters()
            .Where(l => l.AuthStatus == false && l.Status == "PENDING")
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();
    }

    // NEW METHOD - Get approved pending disbursement loans
    public async Task<IEnumerable<Loan>> GetApprovedPendingDisbursementLoansAsync()
    {
        return await _db.Loans.IgnoreQueryFilters()
            .Where(l => l.AuthStatus == true && l.Status == "APPROVED" && l.DisbursedAt == null)
            .OrderByDescending(l => l.AuthorizedAt)
            .ToListAsync();
    }

    //public async Task<DisbursementVoucherDto> DisburseLoanAsync(long loanId, DisbursementRequestDto request)
    //{
    //    try
    //    {
    //        var loan = await GetLoanAsync(loanId);

    //        // Validate loan is approved
    //        if (!loan.AuthStatus)
    //            throw new DomainException($"Loan {loanId} is not approved yet");

    //        //// Validate loan is not already disbursed
    //        //if (loan.DisbursedAt)
    //        //    throw new DomainException($"Loan {loanId} has already been disbursed on {loan.DisbursedAt.Value}");

    //        var account = await _db.Accounts.IgnoreQueryFilters()
    //            .FirstOrDefaultAsync(a => a.AccountId == loan.AccountId)
    //            ?? throw new DomainException($"Account {loan.AccountId} not found");

    //        var cycle = await _db.BiddingCycles.IgnoreQueryFilters()
    //            .FirstOrDefaultAsync(c => c.CycleId == loan.CycleId)
    //            ?? throw new DomainException($"Cycle {loan.CycleId} not found");

    //        // Get scheme config for GL accounts and fees
    //        var scheme = await _db.SchemeConfigs.IgnoreQueryFilters()
    //            .FirstOrDefaultAsync(s => s.TenantId == loan.TenantId)
    //            ?? throw new DomainException($"Scheme config not found for tenant {loan.TenantId}");



    //        // ============================================================
    //        // STEP 1: CALCULATE ALL AMOUNTS
    //        // ============================================================

    //        // Get participants and gross corpus
    //        var participants = await _db.Accounts.IgnoreQueryFilters()
    //            .Where(p => p.TenantId == loan.TenantId
    //                        && (p.Status == AccountStatus.ACTIVE || p.Status == AccountStatus.PRIZED)
    //                        && p.AccountOpenDate <= cycle.CycleMonth
    //                        && p.TenureEndDate > cycle.CycleMonth)
    //            .ToListAsync();

    //        var grossCorpus = participants.Sum(p => p.MonthlyContribution);

    //        // 1.1 Calculate Fixed Rate Amount (if bid has fixed rate)
    //        var fixedRateAmount1 = request.FixedRateAmount;
    //        var fixedRateAmount = request.FixedRateAmount ?? 
    //          Math.Round(grossCorpus * (scheme.FixedRate / 100m), 2);



    //        // 1.2 Calculate Tenant Commission (Org Fee)
    //        var tenantCommission = request.OrgFeeAmount ??
    //            Math.Round(grossCorpus * (scheme.OrgFeePct / 100m), 2);

    //        // 1.3 Calculate SIFIN Commission
    //        var sifinCommission = request.SifinCommission ??
    //            Math.Round(tenantCommission * (scheme.SifinCommissionPct / 100m), 2);

    //        // 1.4 Calculate Processing Fee
    //        var processingFee = request.ProcessingFee ??
    //            Math.Round(loan.PrincipalAmount * (scheme.OrgFeePct / 100m), 2);

    //        // 1.5 Calculate TDS (if applicable - e.g., 10% on commission)
    //        var tdsAmount = request.TdsAmount ??
    //            Math.Round((tenantCommission + sifinCommission) * 0.10m, 2);

    //        // 1.6 Other deductions (if any)
    //        var otherDeductions = request.OtherDeductions ?? 0;

    //        // 1.7 Calculate Net Disbursement Amount
    //        var totalDeductions = fixedRateAmount + tenantCommission + sifinCommission +
    //                              processingFee + tdsAmount + otherDeductions;

    //        var grossAmount = request.Amount ?? loan.PrincipalAmount;
    //        var netAmount = grossAmount - totalDeductions;

    //        if (netAmount < 0) netAmount = 0;

    //        // ============================================================
    //        // STEP 2: UPDATE LOAN WITH ALL CALCULATED VALUES
    //        // ============================================================

    //        loan.Status = "ACTIVE";
    //        loan.DisbursedAt = request.DisbursedAt ?? DateTime.UtcNow;
    //        //loan.DisbursedBy = _ctx.UserId;
    //        loan.OutstandingBalance = netAmount;
    //        loan.NetDisbursementAmount = netAmount;

    //        // Store fee breakdown
    //        //loan.FixedRateAmount = fixedRateAmount;
    //        //loan.TenantCommission = tenantCommission;
    //        //loan.SifinCommission = sifinCommission;
    //        //loan.ProcessingFee = processingFee;
    //        //loan.TdsAmount = tdsAmount;
    //        //loan.OtherDeductions = otherDeductions;
    //        //loan.TotalDeductions = totalDeductions;

    //        //// Store disbursement details
    //        //loan.PaymentMethod = request.PaymentMethod;
    //        //loan.TransactionReference = request.TransactionReference;
    //        //loan.BankName = request.BankName;
    //        //loan.AccountNumber = request.AccountNumber;
    //        //loan.IfscCode = request.IfscCode;
    //        //loan.ChequeNumber = request.ChequeNumber;
    //        //loan.DisbursementRemarks = request.Remarks;

    //        // Generate voucher number
    //        var voucherNumber = GenerateVoucherNumber(cycle.CycleMonth);
    //        //loan.VoucherNumber = voucherNumber;

    //        await _db.SaveChangesAsync();

    //        // Update account balance
    //        account.LoanAmount = (account.LoanAmount) + netAmount;
    //        await _db.SaveChangesAsync();

    //        // ============================================================
    //        // STEP 3: CREATE JOURNAL ENTRIES (VOUCHER)
    //        // ============================================================

    //        var journalLines = new List<JournalLineInput>();
    //        var voucherLines = new List<VoucherLineDto>();

    //        // 3.1 Dr Loans Receivable (Full Principal Amount)
    //        journalLines.Add(new JournalLineInput(
    //            EntryTarget.GL,
    //            scheme.LoanAssetGL,
    //            null,
    //            grossAmount,  // Debit
    //            0
    //        ));
    //        voucherLines.Add(new VoucherLineDto
    //        {
    //            AccountCode = scheme.LoanAssetGL.ToString(),
    //            AccountName = "Loans Receivable",
    //            AccountType = "Asset",
    //            Amount = grossAmount,
    //            Narration = "Loan disbursement principal amount"
    //        });

    //        // 3.2 Cr Bank/Cash (Net Amount)
    //        journalLines.Add(new JournalLineInput(
    //            EntryTarget.GL,
    //            scheme.BankName.ToString(),
    //            null,
    //            0,            // Debit
    //            netAmount     // Credit
    //        ));
    //        voucherLines.Add(new VoucherLineDto
    //        {
    //            AccountCode = scheme.BankName.ToString(),
    //            AccountName = "Bank/Cash Account",
    //            AccountType = "Asset",
    //            Amount = netAmount,
    //            Narration = "Net disbursement to member"
    //        });

    //        // 3.3 Cr Fixed Rate Income (if fixed rate amount > 0)
    //        if (fixedRateAmount > 0)
    //        {
    //            journalLines.Add(new JournalLineInput(
    //                EntryTarget.GL,
    //                scheme.FixedRate.ToString(),
    //                null,
    //                0,                     // Debit
    //                fixedRateAmount       // Credit
    //            ));
    //            voucherLines.Add(new VoucherLineDto
    //            {
    //                //AccountCode = scheme.FixedRateGlId.ToString(),
    //                AccountName = "Fixed Rate Income",
    //                AccountType = "Income",
    //                Amount = fixedRateAmount,
    //                Narration = "Fixed rate income from bidding"
    //            });
    //        }

    //        // 3.4 Cr Tenant Commission (Org Fee)
    //        if (tenantCommission > 0)
    //        {
    //            journalLines.Add(new JournalLineInput(
    //                EntryTarget.GL,
    //                scheme.OrgFeeGlId.ToString(),
    //                null,
    //                0,                     // Debit
    //                tenantCommission       // Credit
    //            ));
    //            voucherLines.Add(new VoucherLineDto
    //            {
    //                AccountCode = scheme.OrgFeeGlId.ToString(),
    //                AccountName = "Organization Fee Income",
    //                AccountType = "Income",
    //                Amount = tenantCommission,
    //                Narration = "Organization fee / Tenant commission"
    //            });
    //        }

    //        // 3.5 Cr SIFIN Commission Payable (if SIFIN commission > 0)
    //        if (sifinCommission > 0)
    //        {
    //            journalLines.Add(new JournalLineInput(
    //                EntryTarget.GL,
    //                scheme.SifinCommissionGlId.ToString(),
    //                null,
    //                0,                     // Debit
    //                sifinCommission        // Credit
    //            ));
    //            voucherLines.Add(new VoucherLineDto
    //            {
    //                AccountCode = scheme.SifinCommissionGlId.ToString(),
    //                AccountName = "SIFIN Commission Payable",
    //                AccountType = "Liability",
    //                Amount = sifinCommission,
    //                Narration = "SIFIN platform commission"
    //            });
    //        }

    //        // 3.6 Cr Processing Fee Income
    //        if (processingFee > 0)
    //        {
    //            journalLines.Add(new JournalLineInput(
    //                EntryTarget.GL,
    //                scheme.OrgFeePct.ToString(),
    //                null,
    //                0,                     // Debit
    //                processingFee          // Credit
    //            ));
    //            voucherLines.Add(new VoucherLineDto
    //            {
    //                //AccountCode = scheme.ProcessingFeeGlId.ToString(),
    //                AccountName = "Processing Fee Income",
    //                AccountType = "Income",
    //                Amount = processingFee,
    //                Narration = "Loan processing fee"
    //            });
    //        }

    //        // 3.7 Cr TDS Payable (if TDS > 0)
    //        if (tdsAmount > 0)
    //        {
    //            journalLines.Add(new JournalLineInput(
    //                EntryTarget.GL,
    //                scheme.Reserve2,
    //                null,
    //                0,                     // Debit
    //                tdsAmount              // Credit
    //            ));
    //            voucherLines.Add(new VoucherLineDto
    //            {
    //                //AccountCode = scheme.TdsGlId.ToString(),
    //                AccountName = "TDS Payable",
    //                AccountType = "Liability",
    //                Amount = tdsAmount,
    //                Narration = "TDS deduction on commission"
    //            });
    //        }

    //        // 3.8 Cr Other Deductions (if any)
    //        if (otherDeductions > 0)
    //        {
    //            journalLines.Add(new JournalLineInput(
    //                EntryTarget.GL,
    //                scheme.SifinPayable,
    //                null,
    //                0,                     // Debit
    //                otherDeductions        // Credit
    //            ));
    //            voucherLines.Add(new VoucherLineDto
    //            {
    //                //AccountCode = scheme.OtherDeductionsGlId.ToString(),
    //                AccountName = "Other Deductions",
    //                AccountType = "Liability",
    //                Amount = otherDeductions,
    //                Narration = "Other deductions"
    //            });
    //        }

    //        // Post journal
    //        await _accounting.PostJournalAsync(
    //            account.TenantId,
    //            cycle.CycleMonth,
    //            JournalSourceType.LOAN_DISBURSEMENT,
    //            sourceId: loan.LoanId,
    //            PaymentMethod.BANK_TRANSFER,
    //            description: $"Loan disbursement voucher - {voucherNumber} - {account.AccountNumber} (cycle {cycle.CycleMonth:yyyy-MM})",
    //            lines: journalLines,
    //            createdBy: _ctx.UserId,
    //            authorizedBy: _ctx.UserId);

    //        // Add ledger entry
    //        _db.LedgerEntries.Add(new LedgerEntry
    //        {
    //            TenantId = account.TenantId,
    //            AccountId = account.AccountId,
    //            CycleId = cycle.CycleId,
    //            EntryType = LedgerEntryType.LOAN_DISBURSEMENT,
    //            Amount = netAmount,
    //            EntryDate = cycle.CycleMonth,
    //            Description = $"Loan disbursed (cycle {cycle.CycleMonth:yyyy-MM}) - Voucher: {voucherNumber}",
    //            CreatedBy = _ctx.UserId
    //        });

    //        await _db.SaveChangesAsync();

    //        // ============================================================
    //        // STEP 4: RETURN VOUCHER DETAILS
    //        // ============================================================

    //        var voucher = new DisbursementVoucherDto
    //        {
    //            VoucherId = loan.LoanId, // Or generate a separate Voucher ID
    //            LoanId = loan.LoanId,
    //            VoucherNumber = voucherNumber,
    //            VoucherDate = loan.DisbursedAt,
    //            TransactionType = "LOAN_DISBURSEMENT",
    //            //PaymentMethod = loan.PaymentMethod,
    //            //TransactionReference = loan.TransactionReference,

    //            GrossAmount = grossAmount,
    //            FixedRateAmount = fixedRateAmount,
    //            TenantCommission = tenantCommission,
    //            SifinCommission = sifinCommission,
    //            ProcessingFee = processingFee,
    //            TdsAmount = tdsAmount,
    //            OtherDeductions = otherDeductions,
    //            NetAmount = netAmount,

    //            DebitEntries = voucherLines.Where(v => v.Amount > 0 && v.AccountType == "Asset"
    //                || v.AccountType == "Expense").ToList(),
    //            CreditEntries = voucherLines.Where(v => v.Amount > 0 && (v.AccountType == "Income"
    //                || v.AccountType == "Liability")).ToList(),

    //            AccountNumber = account.AccountNumber,
    //            //AccountHolder = account.AccountName ?? account.CustomerName,
    //            //BankName = loan.BankName,
    //            //IfscCode = loan.IfscCode,
    //            //Remarks = loan.DisbursementRemarks,
    //            Status = "COMPLETED",
    //            CreatedAt = DateTime.UtcNow,
    //            //CreatedBy = _ctx.UserName,
    //            //AuthorizedBy = _ctx.UserName
    //        };

    //        _log.LogInformation("Disbursed loan {LoanId} ₹{Amount} to account {AccountId}. Voucher: {Voucher}",
    //            loan.LoanId, netAmount, loan.AccountId, voucherNumber);

    //        return voucher;
    //    }
    //    catch (Exception ex)
    //    {
    //        _log.LogError(ex, "Error disbursing loan {LoanId}", loanId);
    //        throw;
    //    }
    //}

    //// Helper method to generate voucher number
    //private string GenerateVoucherNumber(DateOnly cycleMonth)
    //{
    //    var year = cycleMonth.Year;
    //    var month = cycleMonth.Month.ToString("D2");
    //    var sequence = _db.Loans.Count(l => l.AuthorizedBy.HasValue && l.CycleId > 0) + 1;
    //    return $"VCH-{year}{month}-{sequence:D5}";
    //}


    public async Task<DisbursementVoucherDto> DisburseLoanAsync(long loanId, DisbursementRequestDto request)
    {
        try
        {
            var loan = await GetLoanAsync(loanId);

            // Validate loan is approved
            if (!loan.AuthStatus)
                throw new DomainException($"Loan {loanId} is not approved yet");

            var account = await _db.Accounts.IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.AccountId == loan.AccountId)
                ?? throw new DomainException($"Account {loan.AccountId} not found");

            var cycle = await _db.BiddingCycles.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.CycleId == loan.CycleId)
                ?? throw new DomainException($"Cycle {loan.CycleId} not found");

            // Get scheme config for GL accounts and fees
            var scheme = await _db.SchemeConfigs.IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == loan.TenantId)
                ?? throw new DomainException($"Scheme config not found for tenant {loan.TenantId}");

            // ============================================================
            // STEP 1: GET ALL GL ACCOUNT CODES FROM GENERAL LEDGER MASTER
            // ============================================================

            // Collect all GL account names from scheme config
            var glNames = new List<string>();
            if (!string.IsNullOrEmpty(scheme.LoanAssetGL)) glNames.Add(scheme.LoanAssetGL);
            if (!string.IsNullOrEmpty(scheme.BankName)) glNames.Add(scheme.BankName);
            if (!string.IsNullOrEmpty(scheme.Reserve1)) glNames.Add(scheme.Reserve1);
            if (!string.IsNullOrEmpty(scheme.Reserve2)) glNames.Add(scheme.Reserve2);
            if (!string.IsNullOrEmpty(scheme.PoolMoney)) glNames.Add(scheme.PoolMoney);
            if (!string.IsNullOrEmpty(scheme.SifinPayable)) glNames.Add(scheme.SifinPayable);
            if (!string.IsNullOrEmpty(scheme.PenaltyAcc)) glNames.Add(scheme.PenaltyAcc);
            if (!string.IsNullOrEmpty(scheme.NMPenaltyAcc)) glNames.Add(scheme.NMPenaltyAcc);
            if (!string.IsNullOrEmpty(scheme.TdsAc)) glNames.Add(scheme.TdsAc);
            if (!string.IsNullOrEmpty(scheme.ServicesTax)) glNames.Add(scheme.ServicesTax);
            if (!string.IsNullOrEmpty(scheme.GstGl)) glNames.Add(scheme.GstGl);

            // Get GL accounts by their names
            var glAccountsByName = await _db.GeneralLedgerMaster.IgnoreQueryFilters()
                .Where(g => glNames.Contains(g.Name))
                .ToDictionaryAsync(g => g.Name, g => new { g.Code, g.GlId, g.Category });

            // Also get GL accounts by ID for OrgFee and SifinCommission
            var glIds = new List<int?>();
            if (scheme.OrgFeeGlId.HasValue) glIds.Add(scheme.OrgFeeGlId.Value);
            if (scheme.SifinCommissionGlId.HasValue) glIds.Add(scheme.SifinCommissionGlId.Value);

            var glAccountsById = await _db.GeneralLedgerMaster.IgnoreQueryFilters()
                .Where(g => glIds.Contains(g.GlId))
                .ToDictionaryAsync(g => g.GlId, g => new { g.Code, g.Name, g.Category });

            // Helper function to get GL code safely
            string GetGlCode(string glName)
            {
                if (string.IsNullOrEmpty(glName))
                    throw new DomainException($"GL account name is null or empty");

                if (!glAccountsByName.TryGetValue(glName, out var accountInfo))
                    throw new DomainException($"GL account '{glName}' not found in GeneralLedgerMaster");

                return accountInfo.Code;
            }

            string GetGlCodeById(int glId)
            {
                if (!glAccountsById.TryGetValue(glId, out var accountInfo))
                    throw new DomainException($"GL account with ID '{glId}' not found in GeneralLedgerMaster");

                return accountInfo.Code;
            }

            string GetGlNameById(int glId)
            {
                if (!glAccountsById.TryGetValue(glId, out var accountInfo))
                    throw new DomainException($"GL account with ID '{glId}' not found in GeneralLedgerMaster");

                return accountInfo.Name;
            }

            // ============================================================
            // STEP 2: CALCULATE ALL AMOUNTS
            // ============================================================

            // Get participants and gross corpus
            var participants = await _db.Accounts.IgnoreQueryFilters()
                .Where(p => p.TenantId == loan.TenantId
                            && (p.Status == AccountStatus.ACTIVE || p.Status == AccountStatus.PRIZED)
                            && p.AccountOpenDate <= cycle.CycleMonth
                            && p.TenureEndDate > cycle.CycleMonth)
                .ToListAsync();

            var grossCorpus = participants.Sum(p => p.MonthlyContribution);

            // 1.1 Calculate Fixed Rate Amount (if bid has fixed rate)
            var fixedRateAmount = request.FixedRateAmount ??
                Math.Round(grossCorpus * (scheme.FixedRate / 100m), 2);

            // 1.2 Calculate Tenant Commission (Org Fee)
            var tenantCommission = request.OrgFeeAmount ??
                Math.Round(grossCorpus * (scheme.OrgFeePct / 100m), 2);

            // 1.3 Calculate SIFIN Commission
            var sifinCommission = request.SifinCommission ??
                Math.Round(tenantCommission * (scheme.SifinCommissionPct / 100m), 2);

            // 1.4 Calculate Processing Fee
            var processingFee = request.ProcessingFee ??
                Math.Round(loan.PrincipalAmount * (scheme.OrgFeePct / 100m), 2);

            // 1.5 Calculate TDS (if applicable - e.g., 10% on commission)
            var tdsAmount = request.TdsAmount ??
                Math.Round((tenantCommission + sifinCommission) * 0.10m, 2);

            // 1.6 Other deductions (if any)
            var otherDeductions = request.OtherDeductions ?? 0;

            // 1.7 Calculate Net Disbursement Amount
            var totalDeductions = fixedRateAmount + tenantCommission + sifinCommission +
                                  processingFee + tdsAmount + otherDeductions;

            var grossAmount = request.Amount ?? loan.PrincipalAmount;
            var netAmount = grossAmount - totalDeductions;

            if (netAmount < 0) netAmount = 0;

            // ============================================================
            // STEP 3: UPDATE LOAN WITH ALL CALCULATED VALUES
            // ============================================================

            loan.Status = "ACTIVE";
            loan.DisbursedAt = request.DisbursedAt ?? DateTime.UtcNow;
            loan.OutstandingBalance = netAmount;
            loan.NetDisbursementAmount = netAmount;

            // Generate voucher number
            var voucherNumber = GenerateVoucherNumber(cycle.CycleMonth);

            await _db.SaveChangesAsync();

            // Update account balance
            account.LoanAmount = (account.LoanAmount ?? 0) + netAmount;
            await _db.SaveChangesAsync();

            // ============================================================
            // STEP 4: CREATE JOURNAL ENTRIES (VOUCHER) - CORRECT DOUBLE ENTRY
            // ============================================================
            // 
            // ACCOUNTING RULES:
            // 1. Dr Loan Asset (Full amount) 
            //    Cr Customer Account (Full amount)
            // 
            // 2. Dr Customer Account (For deductions)
            //    Cr SIFIN Commission Payable (SIFIN Commission)
            //    Cr Processing Fee Income (Processing Fee)
            //    Cr Fixed Rate Income (Fixed Rate)
            //    Cr Tenant Commission (Org Fee)
            //    Cr TDS Payable (TDS)
            //    Cr Other Deductions (Other)
            // 
            // 3. Dr Bank/Cash (Net amount)
            //    Cr Customer Account (Net amount)
            // ============================================================

            var journalLines = new List<JournalLineInput>();
            var voucherLines = new List<VoucherLineDto>();

            // ---- GET ALL GL CODES ----
            var loanAssetGlCode = GetGlCode(scheme.LoanAssetGL);
            var bankGlCode = GetGlCode(scheme.LoanAssetGL);
            var fixedRateGlCode = GetGlCode(scheme.Reserve1);
            var processingFeeGlCode = GetGlCode(scheme.Reserve2);
            var tdsGlCode = GetGlCode(scheme.TdsAc);
            var otherDeductionsGlCode = GetGlCode(scheme.PoolMoney);

            // Get Org Fee and SIFIN GL codes
            string orgFeeGlCode;
            string orgFeeName;
            if (scheme.OrgFeeGlId.HasValue)
            {
                orgFeeGlCode = GetGlCodeById(scheme.OrgFeeGlId.Value);
                orgFeeName = GetGlNameById(scheme.OrgFeeGlId.Value);
            }
            else
            {
                orgFeeGlCode = GetGlCode(scheme.SifinPayable);
                orgFeeName = scheme.SifinPayable;
            }

            string sifinGlCode;
            string sifinName;
            if (scheme.SifinCommissionGlId.HasValue)
            {
                sifinGlCode = GetGlCodeById(scheme.SifinCommissionGlId.Value);
                sifinName = GetGlNameById(scheme.SifinCommissionGlId.Value);
            }
            else
            {
                sifinGlCode = GetGlCode(scheme.SifinPayable);
                sifinName = scheme.SifinPayable;
            }

            // ---- ENTRY 1: Dr Loan Asset, Cr Customer Account (Full Amount) ----
            // This records the loan receivable from the customer

            // Dr Loan Asset
            journalLines.Add(new JournalLineInput(
                EntryTarget.GL,
                loanAssetGlCode,
                null,
                grossAmount,  // DEBIT
                0
            ));
            voucherLines.Add(new VoucherLineDto
            {
                AccountCode = loanAssetGlCode,
                AccountName = scheme.LoanAssetGL,
                AccountType = "Asset",
                Amount = grossAmount,
                Narration = "Loan disbursement - principal amount",
                //EntryType = "DEBIT"
            });

            // Cr Customer Account (using Member Account)
            journalLines.Add(new JournalLineInput(
                EntryTarget.MEMBER_ACCOUNT,
                null,
                account.AccountId,
                0,  // DEBIT
                grossAmount  // CREDIT
            ));
            voucherLines.Add(new VoucherLineDto
            {
                AccountCode = account.AccountNumber ?? account.AccountId.ToString(),
                AccountName = account.CustomerName ?? "Customer Account",
                AccountType = "Liability",
                Amount = grossAmount,
                Narration = "Loan disbursement - customer account",
                //EntryType = "CREDIT"
            });

            // ---- ENTRY 2: Dr Customer Account (for deductions), Cr Fee Accounts ----
            // This records the deductions from the customer's account

            // 2a. Dr Customer Account (for SIFIN Commission)
            if (sifinCommission > 0)
            {
                journalLines.Add(new JournalLineInput(
                    EntryTarget.MEMBER_ACCOUNT,
                    null,
                    account.AccountId,
                    sifinCommission,  // DEBIT
                    0
                ));
                voucherLines.Add(new VoucherLineDto
                {
                    AccountCode = account.AccountNumber ?? account.AccountId.ToString(),
                    AccountName = account.CustomerName ?? "Customer Account",
                    AccountType = "Liability",
                    Amount = sifinCommission,
                    Narration = "SIFIN commission deduction",
                    //EntryType = "DEBIT"
                });

                // Cr SIFIN Commission Payable
                journalLines.Add(new JournalLineInput(
                    EntryTarget.GL,
                    sifinGlCode,
                    null,
                    0,  // DEBIT
                    sifinCommission  // CREDIT
                ));
                voucherLines.Add(new VoucherLineDto
                {
                    AccountCode = sifinGlCode,
                    AccountName = sifinName,
                    AccountType = "Liability",
                    Amount = sifinCommission,
                    Narration = "SIFIN platform commission payable",
                    //EntryType = "CREDIT"
                });
            }

            // 2b. Dr Customer Account (for Processing Fee)
            if (processingFee > 0)
            {
                journalLines.Add(new JournalLineInput(
                    EntryTarget.MEMBER_ACCOUNT,
                    null,
                    account.AccountId,
                    processingFee,  // DEBIT
                    0
                ));
                voucherLines.Add(new VoucherLineDto
                {
                    AccountCode = account.AccountNumber ?? account.AccountId.ToString(),
                    AccountName = account.CustomerName ?? "Customer Account",
                    AccountType = "Liability",
                    Amount = processingFee,
                    Narration = "Processing fee deduction",
                    //EntryType = "DEBIT"
                });

                // Cr Processing Fee Income
                journalLines.Add(new JournalLineInput(
                    EntryTarget.GL,
                    processingFeeGlCode,
                    null,
                    0,  // DEBIT
                    processingFee  // CREDIT
                ));
                voucherLines.Add(new VoucherLineDto
                {
                    AccountCode = processingFeeGlCode,
                    AccountName = scheme.Reserve2,
                    AccountType = "Income",
                    Amount = processingFee,
                    Narration = "Processing fee income",
                    //EntryType = "CREDIT"
                });
            }

            // 2c. Dr Customer Account (for Fixed Rate)
            if (fixedRateAmount > 0)
            {
                journalLines.Add(new JournalLineInput(
                    EntryTarget.MEMBER_ACCOUNT,
                    null,
                    account.AccountId,
                    fixedRateAmount,  // DEBIT
                    0
                ));
                voucherLines.Add(new VoucherLineDto
                {
                    AccountCode = account.AccountNumber ?? account.AccountId.ToString(),
                    AccountName = account.CustomerName ?? "Customer Account",
                    AccountType = "Liability",
                    Amount = fixedRateAmount,
                    Narration = "Fixed rate amount deduction",
                    //EntryType = "DEBIT"
                });

                // Cr Fixed Rate Income
                journalLines.Add(new JournalLineInput(
                    EntryTarget.GL,
                    fixedRateGlCode,
                    null,
                    0,  // DEBIT
                    fixedRateAmount  // CREDIT
                ));
                voucherLines.Add(new VoucherLineDto
                {
                    AccountCode = fixedRateGlCode,
                    AccountName = scheme.Reserve1,
                    AccountType = "Income",
                    Amount = fixedRateAmount,
                    Narration = "Fixed rate income",
                    //EntryType = "CREDIT"
                });
            }

            // 2d. Dr Customer Account (for Tenant Commission)
            if (tenantCommission > 0)
            {
                journalLines.Add(new JournalLineInput(
                    EntryTarget.MEMBER_ACCOUNT,
                    null,
                    account.AccountId,
                    tenantCommission,  // DEBIT
                    0
                ));
                voucherLines.Add(new VoucherLineDto
                {
                    AccountCode = account.AccountNumber ?? account.AccountId.ToString(),
                    AccountName = account.CustomerName ?? "Customer Account",
                    AccountType = "Liability",
                    Amount = tenantCommission,
                    Narration = "Tenant commission deduction",
                    //EntryType = "DEBIT"
                });

                // Cr Tenant Commission Income
                journalLines.Add(new JournalLineInput(
                    EntryTarget.GL,
                    orgFeeGlCode,
                    null,
                    0,  // DEBIT
                    tenantCommission  // CREDIT
                ));
                voucherLines.Add(new VoucherLineDto
                {
                    AccountCode = orgFeeGlCode,
                    AccountName = orgFeeName,
                    AccountType = "Income",
                    Amount = tenantCommission,
                    Narration = "Tenant commission / Org fee",
                    //EntryType = "CREDIT"
                });
            }

            // 2e. Dr Customer Account (for TDS)
            if (tdsAmount > 0)
            {
                journalLines.Add(new JournalLineInput(
                    EntryTarget.MEMBER_ACCOUNT,
                    null,
                    account.AccountId,
                    tdsAmount,  // DEBIT
                    0
                ));
                voucherLines.Add(new VoucherLineDto
                {
                    AccountCode = account.AccountNumber ?? account.AccountId.ToString(),
                    AccountName = account.CustomerName ?? "Customer Account",
                    AccountType = "Liability",
                    Amount = tdsAmount,
                    Narration = "TDS deduction",
                    //EntryType = "DEBIT"
                });

                // Cr TDS Payable
                journalLines.Add(new JournalLineInput(
                    EntryTarget.GL,
                    tdsGlCode,
                    null,
                    0,  // DEBIT
                    tdsAmount  // CREDIT
                ));
                voucherLines.Add(new VoucherLineDto
                {
                    AccountCode = tdsGlCode,
                    AccountName = scheme.TdsAc,
                    AccountType = "Liability",
                    Amount = tdsAmount,
                    Narration = "TDS payable",
                    //EntryType = "CREDIT"
                });
            }

            // 2f. Dr Customer Account (for Other Deductions)
            if (otherDeductions > 0)
            {
                journalLines.Add(new JournalLineInput(
                    EntryTarget.MEMBER_ACCOUNT,
                    null,
                    account.AccountId,
                    otherDeductions,  // DEBIT
                    0
                ));
                voucherLines.Add(new VoucherLineDto
                {
                    AccountCode = account.AccountNumber ?? account.AccountId.ToString(),
                    AccountName = account.CustomerName ?? "Customer Account",
                    AccountType = "Liability",
                    Amount = otherDeductions,
                    Narration = "Other deductions",
                    //EntryType = "DEBIT"
                });

                // Cr Other Deductions
                journalLines.Add(new JournalLineInput(
                    EntryTarget.GL,
                    otherDeductionsGlCode,
                    null,
                    0,  // DEBIT
                    otherDeductions  // CREDIT
                ));
                voucherLines.Add(new VoucherLineDto
                {
                    AccountCode = otherDeductionsGlCode,
                    AccountName = scheme.PoolMoney,
                    AccountType = "Liability",
                    Amount = otherDeductions,
                    Narration = "Other deductions payable",
                    //EntryType = "CREDIT"
                });
            }

            // ---- ENTRY 3: Dr Bank/Cash, Cr Customer Account (Net Amount) ----
            // This records the actual cash/bank disbursement to the customer

            // Dr Bank/Cash
            journalLines.Add(new JournalLineInput(
                EntryTarget.GL,
                bankGlCode,
                null,
                netAmount,  // DEBIT
                0
            ));
            voucherLines.Add(new VoucherLineDto
            {
                AccountCode = bankGlCode,
                AccountName = scheme.BankName,
                AccountType = "Asset",
                Amount = netAmount,
                Narration = "Net cash disbursement",
                //EntryType = "DEBIT"
            });

            // Cr Customer Account (Net Amount)
            journalLines.Add(new JournalLineInput(
                EntryTarget.MEMBER_ACCOUNT,
                null,
                account.AccountId,
                0,  // DEBIT
                netAmount  // CREDIT
            ));
            voucherLines.Add(new VoucherLineDto
            {
                AccountCode = account.AccountNumber ?? account.AccountId.ToString(),
                AccountName = account.CustomerName ?? "Customer Account",
                AccountType = "Liability",
                Amount = netAmount,
                Narration = "Net disbursement to customer",
                //EntryType = "CREDIT"
            });

            // ============================================================
            // STEP 5: VALIDATE JOURNAL ENTRIES
            // ============================================================

            var totalDebit = journalLines.Sum(l => l.Debit);
            var totalCredit = journalLines.Sum(l => l.Credit);

            _log.LogInformation($"Journal Validation - Total Debit: {totalDebit}, Total Credit: {totalCredit}, Difference: {totalDebit - totalCredit}");

            if (Math.Round(totalDebit, 2) != Math.Round(totalCredit, 2))
            {
                throw new DomainException($"Journal is unbalanced! Debit: {totalDebit}, Credit: {totalCredit}, Difference: {totalDebit - totalCredit}");
            }

            // Post journal
            await _accounting.PostJournalAsync(
                account.TenantId,
                cycle.CycleMonth,
                JournalSourceType.LOAN_DISBURSEMENT,
                sourceId: loan.LoanId,
                PaymentMethod.BANK_TRANSFER,
                description: $"Loan disbursement voucher - {voucherNumber} - {account.AccountNumber} (cycle {cycle.CycleMonth:yyyy-MM})",
                lines: journalLines,
                createdBy: _ctx.UserId,
                authorizedBy: _ctx.UserId);

            // Add ledger entry
            _db.LedgerEntries.Add(new LedgerEntry
            {
                TenantId = account.TenantId,
                AccountId = account.AccountId,
                CycleId = cycle.CycleId,
                EntryType = LedgerEntryType.LOAN_DISBURSEMENT,
                Amount = netAmount,
                EntryDate = cycle.CycleMonth,
                Description = $"Loan disbursed (cycle {cycle.CycleMonth:yyyy-MM}) - Voucher: {voucherNumber}",
                CreatedBy = _ctx.UserId
            });

            await _db.SaveChangesAsync();

            // ============================================================
            // STEP 6: RETURN VOUCHER DETAILS
            // ============================================================

            var voucher = new DisbursementVoucherDto
            {
                VoucherId = loan.LoanId,
                LoanId = loan.LoanId,
                VoucherNumber = voucherNumber,
                VoucherDate = loan.DisbursedAt,
                TransactionType = "LOAN_DISBURSEMENT",

                GrossAmount = grossAmount,
                FixedRateAmount = fixedRateAmount,
                TenantCommission = tenantCommission,
                SifinCommission = sifinCommission,
                ProcessingFee = processingFee,
                TdsAmount = tdsAmount,
                OtherDeductions = otherDeductions,
                NetAmount = netAmount,

                //DebitEntries = voucherLines.Where(v => v.EntryType == "DEBIT").ToList(),
                //CreditEntries = voucherLines.Where(v => v.EntryType == "CREDIT").ToList(),

                AccountNumber = account.AccountNumber,
                Status = "COMPLETED",
                CreatedAt = DateTime.UtcNow,
            };

            _log.LogInformation("Disbursed loan {LoanId} ₹{Amount} to account {AccountId}. Voucher: {Voucher}",
                loan.LoanId, netAmount, loan.AccountId, voucherNumber);

            return voucher;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error disbursing loan {LoanId}", loanId);
            throw;
        }
    }    // Helper method to generate voucher number
    private string GenerateVoucherNumber(DateOnly cycleMonth)
    {
        var year = cycleMonth.Year;
        var month = cycleMonth.Month.ToString("D2");
        var sequence = _db.Loans.Count(l => l.AuthorizedBy.HasValue && l.CycleId > 0) + 1;
        return $"VCH-{year}{month}-{sequence:D5}";
    }
    public async Task<LoanDto> UpdateLoanAsync(int id ,CreateLoanDto updateLoanDto) 
    {
        try 
        {
            // Fetch existing loan
            var loan = await _db.Loans
                .Include(l => l.CoBorrowerDetails)
                .FirstOrDefaultAsync(l => l.LoanId ==id && l.TenantId == _ctx.TenantId.Value);

            if (loan == null)
                throw new DomainException($"Loan with ID {id} not found");

            // Validate status transitions if needed
            if (loan.Status == "CLOSED" || loan.Status == "RESOLVED")
                throw new InvalidOperationException("Cannot update a closed or resolved loan");

            // Update basic loan information
            loan.PrincipalAmount = updateLoanDto.PrincipalAmount;
            loan.DisbursedAt = updateLoanDto.DisbursedAt; 
            //loan.OutstandingBalance = updateLoanDto.OutstandingBalance;
            loan.Status =  loan.Status;
            loan.PhoneNumber = updateLoanDto.PhoneNumber ?? loan.PhoneNumber;
            loan.CustomerName = updateLoanDto.CustomerName ?? loan.CustomerName;
            loan.BidDate = updateLoanDto.BidDate ?? loan.BidDate;
            loan.BranchId = updateLoanDto.BranchId ?? loan.BranchId;
            loan.OrgFeeAmount = updateLoanDto.OrgFeeAmount ?? loan.OrgFeeAmount;
            loan.SifinCommission = updateLoanDto.SifinCommission ?? loan.SifinCommission;
            loan.NetDisbursementAmount = updateLoanDto.NetDisbursementAmount ?? loan.NetDisbursementAmount;
            loan.ProcessingFee = updateLoanDto.ProcessingFee ?? loan.ProcessingFee;
            loan.LoanRemark = updateLoanDto.LoanRemark ?? loan.LoanRemark;
            loan.LoanApplicationStatus = updateLoanDto.LoanApplicationStatus ?? loan.LoanApplicationStatus;
            loan.SecurityDocStatus = updateLoanDto.SecurityDocStatus ?? loan.SecurityDocStatus;
            loan.SecurityDocRemarks = updateLoanDto.SecurityDocRemarks ?? loan.SecurityDocRemarks;
            loan.ChequeObtained = updateLoanDto.ChequeObtained ?? loan.ChequeObtained;
            loan.ChequeAccountNo = updateLoanDto.ChequeAccountNo ?? loan.ChequeAccountNo;
            loan.ChequeBankName = updateLoanDto.ChequeBankName ?? loan.ChequeBankName;
            loan.ChequeNo = updateLoanDto.ChequeNo ?? loan.ChequeNo;
            loan.ChequeDate = updateLoanDto.ChequeDate ?? loan.ChequeDate;
            loan.Remarks = updateLoanDto.Remarks ?? loan.Remarks;
            loan.LoanReleaseStatus = updateLoanDto.LoanReleaseStatus ?? loan.LoanReleaseStatus;
            //loan.AuthStatus = updateLoanDto.AuthStatus;
            //loan.UpdatedAt = DateTime.UtcNow;
            //loan.UpdatedBy = _ctx.UserId;

            // Update Co-Borrowers
            if (updateLoanDto.CoBorrowers != null)
            {
                // Get existing co-borrower IDs
                var existingCoBorrowerIds = loan.CoBorrowerDetails.Select(cb => cb.CoBorrowerId).ToHashSet();
                var updatedCoBorrowerIds = updateLoanDto.CoBorrowers
                    .Where(cb => cb.CoBorrowerId.HasValue)
                    .Select(cb => cb.CoBorrowerId.Value)
                    .ToHashSet();

                // Remove co-borrowers that are not in the update list
                var coBorrowersToRemove = loan.CoBorrowerDetails
                    .Where(cb => !updatedCoBorrowerIds.Contains(cb.CoBorrowerId))
                    .ToList();

                if (coBorrowersToRemove.Any())
                {
                    _db.CoBorrowers.RemoveRange(coBorrowersToRemove);
                }

                // Update or add co-borrowers
                foreach (var cbDto in updateLoanDto.CoBorrowers)
                {
                    if (cbDto.CoBorrowerId.HasValue)
                    {
                        // Update existing co-borrower
                        var existingCoBorrower = loan.CoBorrowerDetails
                            .FirstOrDefault(cb => cb.CoBorrowerId == cbDto.CoBorrowerId.Value);

                        if (existingCoBorrower != null)
                        {
                            existingCoBorrower.CoBorrowerName = cbDto.CoBorrowerName;
                            existingCoBorrower.CoBorrowerPhone = cbDto.CoBorrowerPhone;
                            existingCoBorrower.CoBorrowerEmail = cbDto.CoBorrowerEmail;
                            existingCoBorrower.CoBorrowerAddress = cbDto.CoBorrowerAddress;
                            existingCoBorrower.CoBorrowerAccountNumber = cbDto.CoBorrowerAccountNumber;
                            existingCoBorrower.CoopName = cbDto.CoopName;
                            existingCoBorrower.CoopMobileNumber = cbDto.CoopMobileNumber;
                            existingCoBorrower.CoopAccountId = cbDto.CoopAccountId;
                            existingCoBorrower.CoopAccountNumber = cbDto.CoopAccountNumber;
                            existingCoBorrower.CoBorrowerRemarks = cbDto.CoBorrowerRemarks;
                            existingCoBorrower.IsPrimaryCoBorrower = cbDto.IsPrimaryCoBorrower;
                            existingCoBorrower.CoBorrowerAccountId = cbDto.CoBorrowerAccountId;
                            existingCoBorrower.UpdatedAt = DateTime.UtcNow;
                            existingCoBorrower.UpdatedBy = _ctx.UserId;
                        }
                    }
                    else
                    {
                        // Add new co-borrower
                        var newCoBorrower = new CoBorrower
                        {
                            TenantId = _ctx.TenantId.Value,
                            LoanId = loan.LoanId,
                            CoBorrowerAccountId = cbDto.CoBorrowerAccountId,
                            CoBorrowerName = cbDto.CoBorrowerName,
                            CoBorrowerPhone = cbDto.CoBorrowerPhone,
                            CoBorrowerEmail = cbDto.CoBorrowerEmail,
                            CoBorrowerAddress = cbDto.CoBorrowerAddress,
                            CoBorrowerAccountNumber = cbDto.CoBorrowerAccountNumber,
                            CoopName = cbDto.CoopName,
                            CoopMobileNumber = cbDto.CoopMobileNumber,
                            CoopAccountId = cbDto.CoopAccountId,
                            CoopAccountNumber = cbDto.CoopAccountNumber,
                            CoBorrowerRemarks = cbDto.CoBorrowerRemarks,
                            IsPrimaryCoBorrower = cbDto.IsPrimaryCoBorrower,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = _ctx.UserId,
                            //IsActive = cbDto.IsActive
                        };
                        _db.CoBorrowers.Add(newCoBorrower);
                    }
                }
            }

            await _db.SaveChangesAsync();

            // Create approval request for update
            var approval = new ApprovalRequest
            {
                TenantId = _ctx.TenantId.Value,
                //ActionType = ApprovalActionType.UPDATE_LOAN, // Make sure this enum exists
                EntityType = "Loan Update",
                EntityId = loan.LoanId,
                Payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    loan.LoanId,
                    loan.CycleId,
                    loan.AccountId,
                    loan.AuthStatus,
                    loan.TenantId,
                    //loan.UpdatedBy,
                    loan.PrincipalAmount,
                    loan.DisbursedAt,
                    loan.Status,
                    UpdateTimestamp = DateTime.UtcNow
                }),
                Status = ApprovalStatus.PENDING,
                RequestedBy = _ctx.UserId.Value,
                RequestedAt = DateTime.UtcNow
            };

            _db.ApprovalRequests.Add(approval);
            await _db.SaveChangesAsync();

            _log.LogInformation("Updated loan {LoanId} for account {AccountId}",
                loan.LoanId, loan.AccountId);

            // Return updated loan
            return await GetLoanByIdAsync(loan.LoanId);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error updating loan {LoanId}", updateLoanDto.LoanId);
            throw;
        }
    }

}
