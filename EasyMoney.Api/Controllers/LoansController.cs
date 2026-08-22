using EasyMoney.Api.Auth;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using EasyMoney.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyMoney.Api.Controllers;

[ApiController]
[Route("api/v1/loans")]
[Authorize]
public class LoansController : ControllerBase
{
    private readonly ILoanService _loans;
    public LoansController(ILoanService loans) => _loans = loans;

    // GET /api/v1/loans/by-account/{accountId}
    // Returns the prize record if this account has won a cycle, 404 otherwise.
    [HttpGet("by-account/{accountId:long}")]
    public async Task<ActionResult<LoanDto>> GetByAccount(long accountId)
    {
        var l = await _loans.GetByAccountAsync(accountId);
        if (l is null) return NotFound();
        return Ok(l);
    }

    [HttpGet("summary")]
    [Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.Auditor + "," + Roles.SifinAdmin)]
    public async Task<ActionResult<IReadOnlyList<Loan>>> GetByAccountList()
    {
        var loans = await _loans.GetByAccountbytenant();
        return Ok(loans);
    }

    [HttpPost, Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator + "," + Roles.SifinAdmin)]

    public async Task<ActionResult<LoanDto>> CreateLoan(CreateLoanDto loan)  
    {
        var l = await _loans.CreateLoanAsync(loan);
        if (l is null) return NotFound();
        return Ok(l);
    }

    [HttpPost("{loanId:long}/approve")]
    [Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.SifinAdmin)]
    public async Task<ActionResult<LoanDto>> ApproveLoan(long loanId)
    {
        try
        {
            var loan = await _loans.ApproveLoanAsync(loanId);
            return Ok(new
            {
                message = "Loan approved successfully",
                loan = loan,
                nextStep = "Calculate net amount or disburse"
            });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // GET /api/v1/loans/{loanId}/calculate
    [HttpGet("{loanId:long}/calculate")]
    [Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.Auditor + "," + Roles.SifinAdmin)]
    public async Task<ActionResult<DisbursementCalculationDto>> CalculateDisbursement(long loanId)
    {
        try
        {
            var loan = await _loans.CalculateNetDisbursementAsync(loanId);

            return Ok(loan);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST /api/v1/loans/{loanId}/disburse - THIS IS THE DISBURSEMENT API
    //[HttpPost("{loanId:long}/disburse")]
    //[Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.SifinAdmin)]
    //public async Task<ActionResult<LoanDto>> DisburseLoan(long loanId)
    //{
    //    try
    //    {
    //        var loan = await _loans.DisburseLoanAsync(loanId);
    //        return Ok(new
    //        {
    //            message = "Loan disbursed successfully",
    //            loan = loan,
    //            disbursedAmount = loan.NetDisbursementAmount ?? loan.PrincipalAmount
    //        });
    //    }
    //    catch (DomainException ex)
    //    {
    //        return BadRequest(new { error = ex.Message });
    //    }
    //}
    [HttpPost("{loanId:long}/disburse")]
    [Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.SifinAdmin)]
    public async Task<ActionResult<DisbursementVoucherDto>> DisburseLoan(
        long loanId,
        [FromBody] DisbursementRequestDto request)
    {
        try
        {
            //// Validate request
            //if (request == null)
            //    return BadRequest(new { error = "Request body is required" });

            //if (string.IsNullOrEmpty(request.PaymentMethod))
            //    return BadRequest(new { error = "PaymentMethod is required" });

            //if (string.IsNullOrEmpty(request.TransactionReference))
            //    return BadRequest(new { error = "TransactionReference is required" });

            request.LoanId = loanId;

            var voucher = await _loans.DisburseLoanAsync(loanId, request);

            return Ok(new
            {
                success = true,
                message = "Loan disbursed successfully",
                data = voucher
            });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = "An error occurred while disbursing the loan" });
        }
    }

    // GET /api/v1/loans/pending-approval
    [HttpGet("pending-approval")]
    [Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.SifinAdmin)]
    public async Task<ActionResult<IEnumerable<LoanDto>>> GetPendingApprovalLoans()
    {
        var loans = await _loans.GetPendingApprovalLoansAsync();
        return Ok(loans);
    }

    // GET /api/v1/loans/approved-pending-disbursement
    [HttpGet("approved-pending-disbursement")]
    [Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.SifinAdmin)]
    public async Task<ActionResult<IEnumerable<LoanDto>>> GetApprovedPendingDisbursementLoans()
    {
        var loans = await _loans.GetApprovedPendingDisbursementLoansAsync();
        return Ok(loans);
    }

    //private static LoanDto MapToDto(Loan l)
    //{
    //    return new LoanDto(
    //        l.LoanId,
    //        l.AccountId,
    //        l.CycleId,
    //        l.PrincipalAmount,
    //        l.DisbursedAt,
    //        l.Status.ToString(),
    //        l.AuthStatus,
    //        l.AuthorizedAt,
    //        l.AuthorizedBy,
    //        l.NetDisbursementAmount,
    //        l.ProcessingFee,
    //        l.OrgFeeAmount,
    //        l.SifinCommission,
    //        l.OutstandingBalance,
    //        l.LoanRemark
    //    );
    //}
}
