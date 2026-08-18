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
    Task<Loan> CreateLoanAsync(Loan loan);
    //Task<Loan>
    Task<Loan> ApproveLoanAsync(long loanId); 

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



    public async Task<Loan> CreateLoanAsync(Loan loan)
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
            loan.Status = LoanStatus.ACTIVE;
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

    public Task<Loan?> GetByAccountAsync(long accountId) =>
        _db.Loans.IgnoreQueryFilters().FirstOrDefaultAsync(l => l.AccountId == accountId);

     
    public async Task<Loan> ApproveLoanAsync(long loanId)
    {
        var data = await _db.Loans.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.LoanId == loanId);

        if (data is null)
            throw new DomainException($"Loan {loanId} not found.");

                // Approve this specific bid
        data.AuthStatus = true;
        data.AuthorizedAt = DateTime.UtcNow;
        data.AuthorizedBy = _ctx.UserId;
        data.BranchId = _ctx.BranchId;

        await _db.SaveChangesAsync();

        return data;
    }
}
