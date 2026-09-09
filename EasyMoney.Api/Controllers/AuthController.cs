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
        return Ok(new MeResponse(u.UserId, u.Email, u.Role.ToString(), u.TenantId, u.MemberId, u.IsActive,u.BranchId));
    }
}
