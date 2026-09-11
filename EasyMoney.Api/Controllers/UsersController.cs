using System.Linq;
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
[Route("api/v1")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly EasyMoneyDbContext _db;
    private readonly ITenantContext _ctx;

    public UsersController(IAuthService auth, EasyMoneyDbContext db, ITenantContext ctx)
    {
        _auth = auth; _db = db; _ctx = ctx;
    }

    //static UserDto ToDto(AppUser u) => new(
    //    u.UserId, u.TenantId, u.MemberId, u.Email,
    //    u.Role.ToString(), u.IsActive, u.Member?.Name,u.Tenant?.Name,
    //    u.CreatedBy, u.CreatedAt,
    //    u.AuthorizedBy, u.AuthorizedAt);
    static UserDto ToDto(AppUser u) => new(
    u.UserId,
    u.TenantId,
    u.MemberId,
    u.Email,
    u.Member?.FullName,
    u.UserName,
    u.BranchId,
    u.Branch?.BranchName,
    u.Tenant?.Name,
    u.Role.ToString(),
    u.IsActive,
    u.CreatedBy,
    u.CreatedAt,
    u.AuthorizedBy,
    u.AuthorizedAt,
        u.HintQuestion,
        u.HintAnswerHash);

    // POST /api/v1/users  — create a new user (org or SIFIN-level)
    // SIFIN can create any role; ORG_ADMIN can create org-scoped roles for their own tenant.
    [HttpPost("users"),
     Authorize(Roles = Roles.AnySifin + "," + Roles.OrgAdmin)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
    {
        if (!Enum.TryParse<UserRole>(req.Role, true, out var role))
            return BadRequest(new { error = $"Invalid role '{req.Role}'" });

        bool isSifinRole = role is UserRole.SIFIN_ADMIN or UserRole.SIFIN_OPERATOR or UserRole.SIFIN_AUTHORIZER;

        // Org admin cannot create SIFIN roles
        if (!_ctx.IsSifin && isSifinRole)
            return Forbid();

        // Determine which tenant this user belongs to
        long? tenantId = _ctx.IsSifin ? req.TenantId : _ctx.TenantId;

        // Non-SIFIN roles need a tenant
        if (!isSifinRole && tenantId is null && role != UserRole.AUDITOR)
            return BadRequest(new { error = "tenantId is required for org-level roles" });

        try
        {
            // preAuthorized=true for SIFIN-created users; org-created users may still need MC approval
            bool preAuth = _ctx.IsSifin;
            var u = await _auth.CreateUserAsync(tenantId, req.Email, req.Password, role, req.MemberId,req.BranchId, _ctx.UserId, preAuth, req.UserName, req.HintAnswer , req.HintQuestion);
            var user = await _db.AppUsers
        .Include(x => x.Member)
        .Include(x => x.Branch)
        .Include(x => x.Tenant)
        .IgnoreQueryFilters()
        .FirstAsync(x => x.UserId == u.UserId);

            return CreatedAtAction(nameof(Get), new { userId = u.UserId }, ToDto(u));
        }
        catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // GET /api/v1/users/me — already in AuthController as /auth/me; kept here for convenience
    // GET /api/v1/tenants/{tenantId}/users
    [HttpGet("tenants/{tenantId:long}/users"),
     Authorize(Roles = Roles.AnySifin + "," + Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.Auditor)]
    public async Task<IActionResult> ListByTenant(
        long tenantId,
        [FromQuery] bool? isActive,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        // Org roles may only view their own tenant
        if (!_ctx.IsSifin && _ctx.TenantId != tenantId) return Forbid();

        //var q = _db.AppUsers.IgnoreQueryFilters()
        //    .Where(u => u.TenantId == tenantId);
        var q = _db.AppUsers
    .IgnoreQueryFilters()
    .Include(u => u.Member)
    .Include(u => u.Tenant)
    .Include(x => x.Branch)
    .Where(u => u.TenantId == tenantId);
        if (isActive.HasValue) q = q.Where(u => u.IsActive == isActive.Value);

        var users = await q.OrderBy(u => u.UserId)
            .Skip(skip).Take(Math.Clamp(take, 1, 200))
           //.Select(u => new UserDto(
           //    u.UserId, u.TenantId, u.MemberId, u.Email,
           //    u.Role.ToString(), u.IsActive,
           //    u.CreatedBy, u.CreatedAt,
           //    u.AuthorizedBy, u.AuthorizedAt))
           .Select(u => new UserDto(
    u.UserId,
    u.TenantId,
    u.MemberId,
    u.Email,
    u.Member != null ? u.Member.FullName : null, // Name
    u.UserName,                                  // UserName
    u.BranchId,                                  // BranchId
    u.Branch != null ? u.Branch.BranchName : null, // BranchName
    u.Tenant != null ? u.Tenant.Name : null,     // TenantName
    u.Role.ToString(),
    u.IsActive,
    u.CreatedBy,
    u.CreatedAt,
    u.AuthorizedBy,
    u.AuthorizedAt,
            u.HintQuestion,
        u.HintAnswerHash
    ))
            .ToListAsync();
        return Ok(users);
    }

    // GET /api/v1/users  (SIFIN-only: list all users, optionally filtered by tenant)
    [HttpGet("users"),
     Authorize(Roles = Roles.AnySifin)]
    public async Task<IActionResult> ListAll(
        [FromQuery] long? tenantId,
        [FromQuery] bool? isActive,
        [FromQuery] string? role,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        //var q = _db.AppUsers.IgnoreQueryFilters().AsQueryable();
 
        var q = _db.AppUsers
    .IgnoreQueryFilters()
    .Include(u => u.Member)
    .Include(u => u.Tenant)
    .Include(u => u.Branch)
    .AsQueryable();
        if (tenantId.HasValue) q = q.Where(u => u.TenantId == tenantId.Value);
        if (isActive.HasValue) q = q.Where(u => u.IsActive == isActive.Value);
        if (!string.IsNullOrEmpty(role) && Enum.TryParse<UserRole>(role, true, out var r))
            q = q.Where(u => u.Role == r);

        var users = await q.OrderBy(u => u.UserId)
            .Skip(skip).Take(Math.Clamp(take, 1, 200))
            //.Select(u => new UserDto(
            //    u.UserId, u.TenantId, u.MemberId, u.Email,
            //    u.Role.ToString(), u.IsActive,
            //    u.CreatedBy, u.CreatedAt,
            //    u.AuthorizedBy, u.AuthorizedAt))
            .Select(u => new UserDto(
    u.UserId,
    u.TenantId,
    u.MemberId,
    u.Email,
    u.Member != null ? u.Member.FullName : null, // Name
    u.UserName,                                  // UserName
    u.BranchId,                                  // BranchId
    u.Branch != null ? u.Branch.BranchName : null, // BranchName
    u.Tenant != null ? u.Tenant.Name : null,     // TenantName
    u.Role.ToString(),
    u.IsActive,
    u.CreatedBy,
    u.CreatedAt,
    u.AuthorizedBy,
    u.AuthorizedAt,
            u.HintQuestion,
        u.HintAnswerHash
    
    ))
            .ToListAsync();
        return Ok(users);
    }

    // GET /api/v1/users/{userId}
    [HttpGet("users/{userId:long}"),
     Authorize(Roles = Roles.AnySifin + "," + Roles.OrgAdmin + "," + Roles.OrgAuthorizer + "," + Roles.Auditor)]
    public async Task<IActionResult> Get(long userId)
    {
        //var u = await _db.AppUsers.IgnoreQueryFilters()
        //    .FirstOrDefaultAsync(x => x.UserId == userId);
        var u = await _db.AppUsers
    .IgnoreQueryFilters()
    .Include(x => x.Member)
    .Include(x => x.Tenant)
    .Include(x => x.Branch)
    .FirstOrDefaultAsync(x => x.UserId == userId);
        if (u is null) return NotFound();
        // Org roles may only view users in their own tenant
        if (!_ctx.IsSifin && u.TenantId != _ctx.TenantId) return Forbid();
        //return Ok(new UserDto(
        //    u.UserId, u.TenantId, u.MemberId, u.Email,
        //    u.Role.ToString(), u.IsActive,
        //    u.CreatedBy, u.CreatedAt,
        //    u.AuthorizedBy, u.AuthorizedAt));
        return Ok(new UserDto(
    u.UserId,
    u.TenantId,
    u.MemberId,
    u.Email,
    u.Member?.FullName,          // Name
    u.UserName,                  // UserName
    u.BranchId,                  // BranchId
    u.Branch?.BranchName,        // BranchName
    u.Tenant?.Name,              // TenantName
    u.Role.ToString(),
    u.IsActive,
    u.CreatedBy,
    u.CreatedAt,
    u.AuthorizedBy,
    u.AuthorizedAt,
    u.HintQuestion,
    u.HintAnswerHash ));
    }

    // PUT /api/v1/users/{userId}/status  — activate or deactivate
    [HttpPut("users/{userId:long}/status"),
     Authorize(Roles = Roles.AnySifin + "," + Roles.OrgAdmin + "," + Roles.OrgAuthorizer)]
    public async Task<IActionResult> ChangeStatus(long userId, [FromBody] ChangeUserStatusRequest req)
    {
        // Scope check: org users may only modify users in their own tenant
        var target = await _db.AppUsers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.UserId == userId);
        if (target is null) return NotFound();
        if (!_ctx.IsSifin && target.TenantId != _ctx.TenantId) return Forbid();
        // SIFIN_ADMIN cannot be deactivated via this endpoint (safety guard)
        if (target.Role == UserRole.SIFIN_ADMIN && !req.IsActive)
            return BadRequest(new { error = "Cannot deactivate a SIFIN_ADMIN account" });
            //try
            //{
            //    var u = await _auth.ChangeUserStatusAsync(userId, req.IsActive, _ctx.UserId);
            //    return Ok(new UserDto(
            //        u.UserId, u.TenantId, u.MemberId, u.Email,
            //        u.Role.ToString(), u.IsActive,
            //        u.CreatedBy, u.CreatedAt,
            //        u.AuthorizedBy, u.AuthorizedAt));
            //}

            try
            {
                await _auth.ChangeUserStatusAsync(userId, req.IsActive, _ctx.UserId);

                var u = await _db.AppUsers
                    .IgnoreQueryFilters()
                    .Include(x => x.Member)
                    .Include(x => x.Tenant)
                    .Include(x => x.Branch)
                    .FirstAsync(x => x.UserId == userId);

                return Ok(new UserDto(
        u.UserId,
        u.TenantId,
        u.MemberId,
        u.Email,
        u.Member?.FullName,          // Name
        u.UserName,                  // UserName
        u.BranchId,                  // BranchId
        u.Branch?.BranchName,        // BranchName
        u.Tenant?.Name,              // TenantName
        u.Role.ToString(),
        u.IsActive,
        u.CreatedBy,
        u.CreatedAt,
        u.AuthorizedBy,
        u.AuthorizedAt,
                   u.HintQuestion,
        u.HintAnswerHash

    
        
        
        ));
        }

            catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // PUT /api/v1/users/{userId}/role  — change role (goes through approval when MC enabled)
    // This endpoint applies the change directly; for MC flow use POST /approval-requests with USER_ROLE_CHANGE.
    [HttpPut("users/{userId:long}/role"),
     Authorize(Roles = Roles.AnySifin + "," + Roles.OrgAdmin + "," + Roles.OrgAuthorizer)]
    public async Task<IActionResult> ChangeRole(long userId, [FromBody] ChangeUserRoleRequest req)
    {
        if (!Enum.TryParse<UserRole>(req.NewRole, true, out var newRole))
            return BadRequest(new { error = $"Invalid role '{req.NewRole}'" });

        var target = await _db.AppUsers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.UserId == userId);
        if (target is null) return NotFound();
        if (!_ctx.IsSifin && target.TenantId != _ctx.TenantId) return Forbid();

        // Only SIFIN can assign SIFIN-level roles
        bool newIsSifin = newRole is UserRole.SIFIN_ADMIN or UserRole.SIFIN_OPERATOR or UserRole.SIFIN_AUTHORIZER;
        if (newIsSifin && !_ctx.IsSifin)
            return Forbid();

            //try
            //{
            //    var u = await _auth.ChangeUserRoleAsync(userId, newRole, _ctx.UserId);
            //    return Ok(new UserDto(
            //        u.UserId, u.TenantId, u.MemberId, u.Email,
            //        u.Role.ToString(), u.IsActive,
            //        u.CreatedBy, u.CreatedAt,
            //        u.AuthorizedBy, u.AuthorizedAt));
            //}
            try
            {
                await _auth.ChangeUserRoleAsync(userId, newRole, _ctx.UserId);

                var u = await _db.AppUsers
                    .Include(x => x.Member)
                    .Include(x => x.Tenant)
                    .Include(x => x.Branch)
                    .IgnoreQueryFilters()
                    .FirstAsync(x => x.UserId == userId);

                return Ok(new UserDto(
         u.UserId,
         u.TenantId,
         u.MemberId,
         u.Email,
         u.Member?.FullName,          // Name
         u.UserName,                  // UserName
         u.BranchId,                  // BranchId
         u.Branch?.BranchName,        // BranchName
         u.Tenant?.Name,              // TenantName
         u.Role.ToString(),
         u.IsActive,
         u.CreatedBy,
         u.CreatedAt,
         u.AuthorizedBy,
         u.AuthorizedAt,
              u.HintQuestion,
        u.HintAnswerHash

         ));
            }
            catch (DomainException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
