using EasyMoney.Api.Auth;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using EasyMoney.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EasyMoney.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accounts;
    private readonly ILedgerService _ledger;
    private readonly IExitService _exit;
    private readonly ITenantContext _ctx;

    public AccountsController(IAccountService accounts, ILedgerService ledger, IExitService exit, ITenantContext ctx)
    {
        _accounts = accounts; _ledger = ledger; _exit = exit; _ctx = ctx;
    }

    //[HttpPost("members/{memberId:long}/accounts"),
    // Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator)]
    //public async Task<ActionResult<AccountSummaryDto>> OpenAccount(long memberId, [FromBody] OpenAccountRequest req)
    //{
    //    try
    //    {
    //        var a = await _accounts.OpenAccountAsync(memberId, req.MonthlyContribution, req.AccountOpenDate,
    //            createdBy: _ctx.UserId, authorizedBy: _ctx.UserId );
    //        return Ok(await _accounts.GetSummaryAsync(a.AccountId));
    //    }
    //    catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    //}

    [HttpPost("members/{memberId:long}/accounts")]
    [Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator)]
    public async Task<ActionResult<AccountSummaryDto>> OpenAccount( long memberId,[FromBody] OpenAccountRequest req)
    {
        try
        {
            // Create the payload from the request
            var payload = new AccountOpenPayload(
                MemberId: memberId,
                MonthlyContribution: req.MonthlyContribution,
                AccountOpenDate: req.AccountOpenDate,
                OldAccountNo: req.OldAccountNo,
                PhoneNo: req.PhoneNo,
                CustomerName: req.CustomerName,
                InterestRate: req.InterestRate,
                TargetAmount: req.TargetAmount,
                PaymentDate: req.PaymentDate,
                PaidAmount: req.PaidAmount,
                LoanAmount: req.LoanAmount,
                BonusAmount: req.BonusAmount,
                InterestAmount: req.InterestAmount,
                TotalAmount: req.TotalAmount,
                Remarks: req.Remarks
            );

            var a = await _accounts.OpenAccountAsync(
                memberId: memberId,
                monthlyContribution: req.MonthlyContribution,
                openDate: req.AccountOpenDate,
                createdBy: _ctx.UserId,
                authorizedBy: _ctx.UserId,
                payload  // Pass the payload
            );

            return Ok(await _accounts.GetSummaryAsync(a.AccountId));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("members/{memberId:long}/accounts")]
    public async Task<ActionResult<IReadOnlyList<AccountSummaryDto>>> ListByMember(long memberId) =>
        Ok(await _accounts.ListByMemberAsync(memberId));

    [HttpGet("accounts/{accountId:long}/dues")]
    public async Task<ActionResult<IReadOnlyList<DueLineDto>>> GetDues(long accountId)
    {
        try { return Ok(await _ledger.GetDuesAsync(accountId)); }
        catch (DomainException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpGet("accounts")]
    public async Task<ActionResult<IReadOnlyList<AccountSummaryDto>>> List(
        [FromQuery] int skip = 0, [FromQuery] int take = 50) =>
        Ok(await _accounts.ListByTenantAsync(skip, take));

    [HttpGet("accounts/{accountId:long}")]
    public async Task<ActionResult<AccountSummaryDto>> Get(long accountId)
    {
        try { return Ok(await _accounts.GetSummaryAsync(accountId)); }
        catch (DomainException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpPost("accounts/{accountId:long}/payments"),
     Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator)]
    public async Task<ActionResult<PaymentResultDto>> RecordPayment(long accountId, [FromBody] RecordPaymentRequest req)
    {
        if (!Enum.TryParse<PaymentMethod>(req.Method, true, out var method))
            return BadRequest(new { error = "Invalid payment method" });
        try { return Ok(await _ledger.RecordPaymentAsync(accountId, req, method)); }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("accounts/{accountId:long}/exit"),
     Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgAuthorizer)]
    public async Task<ActionResult<ExitResultDto>> Exit(long accountId,
        [FromQuery] DateOnly? exitDate, [FromQuery] string? refundMethod)
    {
        var d = exitDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var m = PaymentMethod.BANK_TRANSFER;
        if (!string.IsNullOrEmpty(refundMethod) && !Enum.TryParse<PaymentMethod>(refundMethod, true, out m))
            return BadRequest(new { error = "Invalid refundMethod" });
        try { return Ok(await _exit.ProcessExitAsync(accountId, d, m)); }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
