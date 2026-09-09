using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace EasyMoney.Api.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(string email, string password);
    Task<LoginResponse> RefreshAsync(string refreshTokenRaw);
    Task LogoutAsync(string refreshTokenRaw);
    Task<AppUser> CreateUserAsync(
        long? tenantId, string email, string password, UserRole role, long? memberId,
        long? branchId, long? createdBy, bool preAuthorized);
    Task<AppUser> ChangeUserStatusAsync(long userId, bool isActive, long? changedBy);
    Task<AppUser> ChangeUserRoleAsync(long userId, UserRole newRole, long? changedBy);
}
public class AuthService : IAuthService
{
    private readonly EasyMoneyDbContext _db;
    private readonly ITokenService _tokens;
    private readonly JwtSettings _jwt;
    private readonly ITenantContext _tenantCtx;



    public AuthService(EasyMoneyDbContext db, ITokenService tokens, JwtSettings jwt, ITenantContext tenantCtx)
    {
        _db = db; _tokens = tokens; _jwt = jwt; _tenantCtx = tenantCtx; ;
    }

    public async Task<LoginResponse> LoginAsync(string email, string password)
    {
        // app_user is global (no tenant filter). Use IgnoreQueryFilters defensively.
        var user = await _db.AppUsers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !user.IsActive)
            throw new DomainException("Invalid credentials");
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new DomainException("Invalid credentials");

        // Users requiring maker-checker (USER_CREATE / USER_ROLE_CHANGE) cannot
        // log in until authorized_by is stamped. Seeded SIFIN users are pre-authorized.
        if (user.AuthorizedAt is null && user.Role is not UserRole.SIFIN_ADMIN)
            throw new DomainException("User pending authorization");
        var access = _tokens.CreateAccessToken(user);
        var (raw, hash) = _tokens.CreateRefreshToken();
        var expires = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays);
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = hash,
            ExpiresAt = expires
        });
        await _db.SaveChangesAsync();
        //var tenantName = await GetTenantNameAsync(user.TenantId);
        //var tenantname = tenant.name;
        //var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.TenantId == user.TenantId || null);
        //var tenantName = tenant.Name;

        string tenantName = GetTenantNameForUser(user);
        var branch = GetBranchForUser(user);
        //string? tenantName = null;
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

        return new LoginResponse(
                access, raw, DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes),
                user.UserId, user.Email, user.Role.ToString(),
                user.TenantId, user.MemberId, tenantName, user.BranchId, branchName, previousDate, currentDate, nextDate);
    }

    public async Task<LoginResponse> RefreshAsync(string refreshTokenRaw)
    {
        var hash = _tokens.HashRefreshToken(refreshTokenRaw);
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash)
            ?? throw new DomainException("Invalid refresh token");
        if (existing.RevokedAt is not null || existing.ExpiresAt <= DateTime.UtcNow)
            throw new DomainException("Refresh token expired or revoked");

        var user = await _db.AppUsers.IgnoreQueryFilters().FirstAsync(u => u.UserId == existing.UserId);
        if (!user.IsActive) throw new DomainException("User is inactive");

        // Rotate: revoke the old token, issue a new pair.
        existing.RevokedAt = DateTime.UtcNow;
        var access = _tokens.CreateAccessToken(user);
        var (raw, newHash) = _tokens.CreateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = newHash,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays)
        });
        await _db.SaveChangesAsync();

        //var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.TenantId == user.TenantId);
        //var tenantName = tenant.Name;
        string tenantName = GetTenantNameForUser(user);
        var branch = GetBranchForUser(user);
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

        return new LoginResponse(
                access, raw, DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes),
                user.UserId, user.Email, user.Role.ToString(),
                 user.TenantId, user.MemberId, tenantName, user.BranchId, branchName, previousDate, currentDate, nextDate);

        //user.TenantId, user.MemberId, tenantName, user.BranchId, branch.BranchName, branch.PreviousDate, branch.CurrentDate, branch.NextDate);
    }

    public async Task LogoutAsync(string refreshTokenRaw)
    {
        var hash = _tokens.HashRefreshToken(refreshTokenRaw);
        var t = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash);
        if (t is not null && t.RevokedAt is null)
        {
            t.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<AppUser> CreateUserAsync(
        long? tenantId, string email, string password, UserRole role, long? memberId,
        long? branchId, long? createdBy, bool preAuthorized)
    {
        var exists = await _db.AppUsers.IgnoreQueryFilters().AnyAsync(u => u.Email == email);
        if (exists) throw new DomainException("Email already in use");

        // SIFIN roles have tenant_id NULL by definition. Org roles must have a tenant.
        bool isSifinRole = role is UserRole.SIFIN_ADMIN or UserRole.SIFIN_OPERATOR or UserRole.SIFIN_AUTHORIZER;
        if (isSifinRole && tenantId.HasValue)
            throw new DomainException("SIFIN-level users cannot be tenant-scoped");
        if (!isSifinRole && !tenantId.HasValue && role != UserRole.AUDITOR)
            throw new DomainException("Tenant-level users require tenant_id");

        var u = new AppUser
        {
            TenantId = tenantId,
            MemberId = memberId,
            BranchId = branchId,
            Email = email,
            UserName = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password), Role = role,
            CreatedBy = createdBy,
            IsActive = true,
            AuthorizedBy = preAuthorized ? createdBy : null,
            AuthorizedAt = preAuthorized ? DateTime.UtcNow : null
        };
        _db.AppUsers.Add(u);
        await _db.SaveChangesAsync();
        return u;
    }

    public async Task<AppUser> ChangeUserStatusAsync(long userId, bool isActive, long? changedBy)
    {
        var u = await _db.AppUsers.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.UserId == userId)
            ?? throw new DomainException($"User {userId} not found");
        u.IsActive = isActive;
        // If re-activating and user was never authorized, stamp authorization now
        if (isActive && u.AuthorizedAt is null)
        {
            u.AuthorizedBy = changedBy;
            u.AuthorizedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
        return u;
    }

    public async Task<AppUser> ChangeUserRoleAsync(long userId, UserRole newRole, long? changedBy)
    {
        var u = await _db.AppUsers.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.UserId == userId)
            ?? throw new DomainException($"User {userId} not found");

        bool newIsSifin = newRole is UserRole.SIFIN_ADMIN or UserRole.SIFIN_OPERATOR or UserRole.SIFIN_AUTHORIZER;
        bool wasOrgRole = u.TenantId.HasValue;
        if (newIsSifin && wasOrgRole)
            throw new DomainException("Cannot promote a tenant-scoped user to a SIFIN role");

        u.Role = newRole;
        u.AuthorizedBy = changedBy;
        u.AuthorizedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return u;
    }
    private string GetTenantNameForUser(AppUser user)
    {
        // Super Admin or users without tenant
        if (!user.TenantId.HasValue)
            return "System"; // or "Super Admin" or "Global"

        // Tenant user - try to get tenant name
        var tenant = _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefault(t => t.TenantId == user.TenantId.Value);

        return tenant?.Name ?? "Unknown Tenant";
    }
    //public string GetBranchForUser(AppUser user)
    //{
    //    if (!user.TenantId.HasValue)
    //        return "System";

    //    var tenant = _db.Branches
    //        .IgnoreQueryFilters()
    //        .FirstOrDefault(t => t.BranchId == user.BranchId);

    //    return tenant?.BranchName ?? "Unknown Tenant";
    //}

        public Branch GetBranchForUser(AppUser user)
        {
            if (!user.BranchId.HasValue)
                return null;

            var branch = _db.Branches
                .IgnoreQueryFilters()
                .FirstOrDefault(t => t.BranchId == user.BranchId);

            return branch;
        }

}

