using System.Security.Claims;
using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using EasyMoney.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyMoney.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly EasyMoneyDbContext _db;

    public AuthController(IAuthService auth, EasyMoneyDbContext db)
    {
        _auth = auth; _db = db;
    }

    [HttpPost("login"), AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
    {
        try { return Ok(await _auth.LoginAsync(req.Email, req.Password)); }
        catch (DomainException ex) { return Unauthorized(new { error = ex.Message }); }
    }

    [HttpPost("refresh"), AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Refresh(RefreshRequest req)
    {
        try { return Ok(await _auth.RefreshAsync(req.RefreshToken)); }
        catch (DomainException ex) { return Unauthorized(new { error = ex.Message }); }
    }

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout(LogoutRequest req)
    {
        await _auth.LogoutAsync(req.RefreshToken);
        return NoContent();
    }

    [HttpGet("me"), Authorize]
    public async Task<ActionResult<MeResponse>> Me()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(idStr, out var userId)) return Unauthorized();
        var u = await _db.AppUsers.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.UserId == userId);
        if (u is null) return Unauthorized();
        var branch = _db.Branches.IgnoreQueryFilters().FirstOrDefault(b => b.BranchId == u.BranchId);
        string? branchName = null;
        DateOnly? previousDate = null;
        DateOnly? currentDate = null;
        DateOnly? nextDate = null;
        if (branch != null)
        {
            branchName = branch.BranchName;
            previousDate = branch.PreviousDate;
            currentDate = branch.CurrentDate;
            nextDate = branch.NextDate;
        }
        return Ok(new MeResponse(u.UserId, u.Email, u.Role.ToString(), u.TenantId, u.MemberId, u.IsActive,u.BranchId, branchName,
           previousDate, currentDate, nextDate));
    }

    [HttpPut("change-password"), Authorize]
    public async Task<ActionResult<ChangePasswordResponse>> ChangePassword(ChangePasswordRequest req)
    {
        try
        {
            await _auth.ChangePasswordAsync(req.CurrentPassword, req.NewPassword);
            return Ok(new ChangePasswordResponse("Password changed successfully. Please log in again."));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("hint-question"), AllowAnonymous]
    public async Task<ActionResult<HintQuestionResponse>> GetHintQuestion(HintQuestionRequest req)
    {
        try
        {
            return Ok(await _auth.GetHintQuestionAsync(req.Identifier));
        }
        catch (DomainException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("verify-user"), AllowAnonymous]
    public async Task<ActionResult<VerifyUserResponse>> VerifyUser(VerifyUserRequest req)
    {
        try
        {
            return Ok(await _auth.VerifyUserAsync(req.Identifier, req.HintAnswer,req.HintQuestion));
        }
        catch (DomainException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    // ---------- Step 3: reset password with token + retype ----------
    [HttpPost("forgot-password"), AllowAnonymous]
    public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword(ForgotPasswordRequest req)
    {
        try
        {
            await _auth.ResetForgotPasswordAsync(
                req.Identifier, req.ResetToken, req.NewPassword, req.RetypePassword);
            return Ok(new ForgotPasswordResponse(
                "Password reset successfully. Please log in."));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }


}
