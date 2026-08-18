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
        return Ok(new LoanDto(l.LoanId, l.AccountId, l.CycleId, l.PrincipalAmount, l.DisbursedAt));
    }



    [HttpPost, Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator + "," + Roles.SifinAdmin)]

    public async Task<ActionResult<LoanDto>> CreateLoan(Loan loan)  
    {
        var l = await _loans.CreateLoanAsync(loan);
        if (l is null) return NotFound();
        return Ok(new LoanDto(l.LoanId, l.AccountId, l.CycleId, l.PrincipalAmount, l.DisbursedAt));
    }
}
