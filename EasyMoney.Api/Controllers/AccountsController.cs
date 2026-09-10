using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using EasyMoney.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Ocsp;

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
    private readonly EasyMoneyDbContext _db;

    public AccountsController(EasyMoneyDbContext db, IAccountService accounts, ILedgerService ledger, IExitService exit, ITenantContext ctx)
    {
        _accounts = accounts; _ledger = ledger; _exit = exit; _ctx = ctx; _db = db;
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

            var existingAccount = await _accounts.GetMemberAsync(req.SchemeId, req.MemberId);

            // If we get here, account exists - return error
            if (existingAccount != null)
            {
                return BadRequest(new
                {
                   message = $"Account already exists,Please Select Other Member ID "
                    //existingAccount = existingAccount
                });
            }

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
                Remarks: req.Remarks,
                SchemeId: req.SchemeId,
                BranchId: _ctx.BranchId,
                IsBidding :"N",
                FirstPayment:"Y",
                CustomerCode : req.CustomerCode

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

    //[HttpPost("accounts/{accountId:long}/payments"),
    // Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator)]
    //public async Task<ActionResult<PaymentResultDto>> RecordPayment(long accountId, [FromBody] RecordPaymentRequest req) //chequeDate
    //{ 
    //    if (!Enum.TryParse<PaymentMethod>(req.Method, true, out var method))
    //        return BadRequest(new { error = "Invalid payment method" });
    //    try { return Ok(await _ledger.RecordPaymentAsync(accountId, req.Amount, req.PaidDate, method, req.DueId ,req.GlCode,req.ChequeNo,req.AccountNo,req.BankName,req.UpiId ,req.Chequedate)); }
    //    catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    //}

    [HttpPost("accounts/{accountId:long}/payments")]
    [Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator)]
    public async Task<ActionResult<PaymentResultDto>> RecordPayment(
    long accountId,
    [FromBody] RecordPaymentRequest req)
    {
        // Validate request
        if (req == null)
            return BadRequest(new { error = "Request body cannot be empty" });

        // Parse payment method
        if (!Enum.TryParse<PaymentMethod>(req.Method, true, out var method))
            return BadRequest(new { error = "Invalid payment method" });

        try
        {
            var result = await _ledger.RecordPaymentAsync(
                accountId,
                req.Amount,
                req.PaidDate,
                method,
                req.DueId,
                req.GlCode,
                req.ChequeNo,
                req.AccountNo,
                req.BankName,
                req.UpiId,
                req.Chequedate
           );

            return Ok(result);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            // Log the exception
            //_logger.LogError(ex, "Error recording payment for account {AccountId}", accountId);
            return StatusCode(500, new { error = "An unexpected error occurred" });
        }
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

    // In your controller, don't include AccountNumber in the update
    [HttpPut("members/{memberId:long}/accounts/{accountId:long}")]
    [Authorize(Roles = Roles.OrgAdmin + "," + Roles.OrgOperator)]
    public async Task<IActionResult> UpdateAccount(
       long memberId,
       long accountId,
       [FromBody] UpdateAccountRequest req)
    {
        try
        {
            // Get the existing account to use its account number
            var existingAccount = await _accounts.GetSummaryAsync(accountId);

            var payload = new AccountUpdatePayload(
                MemberId: memberId,
                MonthlyContribution: req.MonthlyContribution,
                AccountOpenDate: req.AccountOpenDate,
                AccountNumber: existingAccount.AccountNumber, // Use existing account number
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
                Remarks: req.Remarks,
                //SchemeId: req.SchemeId,
                TenureEndDate: req.TenureEndDate,
                Status: req.Status,
                InstallmentsPaid: req.InstallmentsPaid,
                IsPrized: req.IsPrized,
                ClosureDate: req.ClosureDate,
                SchemeId:existingAccount.SchemeId


            );

            var updatedAccount = await _accounts.UpdateAccountAsync(
                accountId: accountId,
                memberId: memberId,
                monthlyContribution: req.MonthlyContribution,
                openDate: req.AccountOpenDate,
                updateBy: _ctx.UserId,
                authorizedBy: _ctx.UserId, // Fix: Added the missing 'authorizedBy' argument
                payload: payload
            );

            return Ok(await _accounts.GetSummaryAsync(updatedAccount.AccountId));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }


    //public async Task<ActionResult<AccountSummaryDto>> GetAccountClsoure(long accountId)
    //{ 
    //    try { return Ok(await _accounts.GetSummaryAsync(accountId)); }
    //    catch (DomainException ex) { return NotFound(new { error = ex.Message }); }
    //}
    [HttpPost("accounts/{accountId:long}/closure")]

    public async Task<ActionResult<AccountClosureResponse>> GetAccountClsoure([FromBody] AccountClosureRequest request)
    {
        try
        {
            // Validate request
            if (request == null)
            {
                return BadRequest(new { message = "Invalid request" });
            }

            var account = await _accounts.GetAccountAsync(request.AccountId);
            if (account == null)
            {
                return NotFound(new { message = "Account not found" });
            }
            if (account.Status == "CLOSED" || account.Status == "CLOSURE")
            {
                return BadRequest(new { message = "Account is already closed" });
            }

         var accdata =   await _accounts.ClosedAccountAsync(request);     


            return Ok(accdata);
        }
        catch (Exception ex)
        {
            //_logger.LogError(ex, "Error closing account");
            return StatusCode(500, new { message = "Failed to close account", error = ex.Message });
        }
    }
    [HttpGet("accounts/closure")]
    public async Task<ActionResult<IReadOnlyList<AccountSummaryDto>>> AccountclosedList(
    [FromQuery] int skip = 0, [FromQuery] int take = 50) =>
    Ok(await _accounts.ListByAccountclosed(skip, take));



    [HttpPut("accounts/{accountId:long}/periodchange")]
    public async Task<ActionResult<Account>> UpdatePeriodChange(
          long accountId,
          [FromBody] PeriodChangeRequest request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new { error = "Invalid request" });
            }

            // Validate account ID match
            if (request.AccountId != accountId)
            {
                return BadRequest(new { error = "Account ID mismatch" });
            }

            // Validate required fields
            if (request.MonthlyContribution <= 0)
            {
                return BadRequest(new { error = "Monthly Contribution must be greater than 0" });
            }

            if (request.Period <= 0)
            {
                return BadRequest(new { error = "Period must be greater than 0" });
            }

            var result = await _accounts.UpdatePeriodChangeAsync(accountId, request);

            return Ok(new
            {
                success = true,
                message = "Account period updated successfully",
                data = result
            });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "An error occurred while updating the account period" });
        }
    }
     
    [HttpPost("sifin-commission")]
    public async Task<ActionResult<SifinCommission>> SaveSiffinCommssion ([FromBody] SifinCommission request) 
    {
        try
        {
            request.TenantId=_ctx.TenantId;
            request.BranchId = _ctx.BranchId;
            var accdata = await _accounts.SifinAccountAsync(request);
            return Ok(accdata);
        }
        catch (Exception ex)
        {
            //_logger.LogError(ex, "Error closing account");
            return StatusCode(500, new { message = "Failed to close account", error = ex.Message });
        }
    }

    [HttpGet("sifin-commission-list")]
    public async Task<ActionResult<SifinCommission>> SaveSiffinCommssiondata()
    {
        try
        {
            var accdata = await _accounts.GetsifinSummaryAsync();
            return Ok(accdata);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to close account", error = ex.Message });
        }
    }
}
