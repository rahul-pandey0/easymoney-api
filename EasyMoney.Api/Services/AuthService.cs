using EasyMoney.Api.Auth;
using EasyMoney.Api.Data;
using EasyMoney.Api.Domain;
using EasyMoney.Api.Dtos;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Ocsp;
using Serilog;
using System.Security.Claims; 
using System.Security.Cryptography;
using System.Text;

namespace EasyMoney.Api.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(string email, string password);
    Task<LoginResponse> RefreshAsync(string refreshTokenRaw);
    Task LogoutAsync(string refreshTokenRaw);
    Task<AppUser> CreateUserAsync(
        long? tenantId, string email, string password, UserRole role, long? memberId,
        long? branchId, long? createdBy, bool preAuthorized ,string ? UserName ,string? HintAnswerHash, string? HintQuestion);
    Task<AppUser> ChangeUserStatusAsync(long userId, bool isActive, long? changedBy);
    Task<AppUser> ChangeUserRoleAsync(long userId, UserRole newRole, long? changedBy);

    Task ChangePasswordAsync( string currentPassword, string newPassword);



    //new upadtes
    Task ResetPasswordAsync(long targetUserId, string newPassword, long? resetBy);

    Task<HintQuestionResponse> GetHintQuestionAsync(string identifier);
    Task<VerifyUserResponse> VerifyUserAsync(string identifier, string hintAnswer ,string HintQuestion);
    Task ResetForgotPasswordAsync(string identifier, string resetToken,string newPassword, string retypePassword);
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
        string tenantName = GetTenantNameForUser(user);
        var branch = GetBranchForUser(user);

        var claims = new List<Claim>
        {
            new Claim("branch_name", branch?.BranchName ?? string.Empty),
            new Claim("previous_date", branch?.PreviousDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd")),
            new Claim("current_date", branch?.CurrentDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd")),
            new Claim("next_date", branch?.NextDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"))
        };



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
        long? branchId, long? createdBy, bool preAuthorized ,string? UserName,string? HintAnswerHash ,string ? HintQuestion
)
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
            UserName = UserName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password), Role = role,
            CreatedBy = createdBy,
            IsActive = true,
            AuthorizedBy = preAuthorized ? createdBy : null,
            AuthorizedAt = preAuthorized ? DateTime.UtcNow : null,
            HintAnswerHash=HintAnswerHash,
            HintQuestion=HintQuestion,
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

    public async Task ChangePasswordAsync(string currentPassword, string newPassword) 
    {
        //ValidatePasswordStrength(newPassword);
        long? userId = _tenantCtx.UserId; 

        var user = await _db.AppUsers.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.UserId == _tenantCtx.UserId)
            ?? throw new DomainException("User not found");

        if (!user.IsActive)
            throw new DomainException("User is inactive");

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            throw new DomainException("Current password is incorrect");

        if (BCrypt.Net.BCrypt.Verify(newPassword, user.PasswordHash))
            throw new DomainException("New password must be different from current password");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;     

        var activeTokens = await _db.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null)
            .ToListAsync();
        foreach (var t in activeTokens)
            t.RevokedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        Log.Information("User {UserId} changed their password", userId);
    }



    public async Task<HintQuestionResponse> GetHintQuestionAsync(string identifier)
    {
        var user = await FindByIdentifierAsync(identifier);
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.HintQuestion))
            throw new DomainException("User not found or hint question not configured");

        return new HintQuestionResponse(user.HintQuestion);
    }
    public async Task<VerifyUserResponse> VerifyUserAsync(string identifier, string hintAnswer, string HintQuestion)
    {
        var user = await FindByIdentifierAsync(identifier);
        if (user is null || !user.IsActive)
            throw new DomainException("Invalid credentials");

        if (string.IsNullOrWhiteSpace(user.HintQuestion) ||
            string.IsNullOrWhiteSpace(user.HintAnswerHash))
            throw new DomainException("Hint question not configured for this user");

        // Normalize the answer the same way it was stored (lowercase + trim)
        var normalized = (hintAnswer ?? string.Empty).Trim().ToLowerInvariant();



        // Fix for CS0234: Replace the incorrect namespace 'BCrypt' with 'BCrypt.Net.BCrypt'  
        user.HintAnswerHash = BCrypt.Net.BCrypt.HashPassword(hintAnswer.Trim().ToLowerInvariant());

        // Fix for CS8602: Add a null check for 'hintAnswer' before accessing its methods  
        if (string.IsNullOrWhiteSpace(hintAnswer))
            throw new DomainException("Hint answer cannot be null or empty");

        user.HintAnswerHash = BCrypt.Net.BCrypt.HashPassword(hintAnswer.Trim().ToLowerInvariant());

        if (!SafeBCryptVerify(normalized, user.HintAnswerHash))
            throw new DomainException("Invalid credentials");

        // Invalidate any previous unused tokens for this user
        var oldTokens = await _db.PasswordResetTokens
            .Where(t => t.UserId == user.UserId && t.UsedAt == null)
            .ToListAsync();
        foreach (var t in oldTokens) t.UsedAt = DateTime.UtcNow;

        // Issue new one-time token (reuse the refresh-token generator)
        var (raw, hash) = _tokens.CreateRefreshToken();
        var expires = DateTime.UtcNow.AddMinutes(10);

        _db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.UserId,
            TokenHash = hash,
            ExpiresAt = expires
        });
        await _db.SaveChangesAsync();

        Log.Information("Hint verification succeeded for user {UserId}", user.UserId);
        return new VerifyUserResponse(raw, expires);
    }

    public async Task ResetForgotPasswordAsync(
        string identifier, string resetToken, string newPassword, string retypePassword)
    {
        if (string.IsNullOrWhiteSpace(resetToken))
            throw new DomainException("Invalid or expired verification token");

        if (newPassword != retypePassword)
            throw new DomainException("New password and retype password do not match");

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            throw new DomainException("Password must be at least 8 characters long");

        var user = await FindByIdentifierAsync(identifier);
        if (user is null || !user.IsActive)
            throw new DomainException("Invalid credentials");

        var hash = _tokens.HashRefreshToken(resetToken);

        var record = await _db.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.UserId == user.UserId)
            ?? throw new DomainException("Invalid or expired verification token");

        if (record.UsedAt is not null || record.ExpiresAt <= DateTime.UtcNow)
            throw new DomainException("Invalid or expired verification token");

        if (BCrypt.Net.BCrypt.Verify(newPassword, user.PasswordHash))
            throw new DomainException("New password must be different from current password");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        record.UsedAt = DateTime.UtcNow;

        // Revoke all active refresh tokens
        var activeTokens = await _db.RefreshTokens
            .Where(r => r.UserId == user.UserId && r.RevokedAt == null)
            .ToListAsync();
        foreach (var t in activeTokens) t.RevokedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        Log.Information("Password reset (forgot flow) completed for user {UserId}", user.UserId);
    }

    // Helper — accepts email OR username
    private Task<AppUser?> FindByIdentifierAsync(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return Task.FromResult<AppUser?>(null);

        var id = identifier.Trim();
        return _db.AppUsers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == id || u.UserName == id);
    }

    public async Task ResetPasswordAsync(long targetUserId, string newPassword, long? resetBy)
    {
        //ValidatePasswordStrength(newPassword);

        var user = await _db.AppUsers.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.UserId == targetUserId)
            ?? throw new DomainException($"User {targetUserId} not found");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        var activeTokens = await _db.RefreshTokens.Where(r => r.UserId == targetUserId && r.RevokedAt == null).ToListAsync();
        foreach (var t in activeTokens)
            t.RevokedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        Log.Information("User {ResetBy} reset password for user {TargetUserId}", resetBy, targetUserId);
    }

    //private static void ValidatePasswordStrength(string password)
    //{
    //    if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
    //        throw new DomainException("Password must be at least 8 characters long");
    //    if (!password.Any(char.IsUpper))
    //        throw new DomainException("Password must contain at least one uppercase letter");
    //    if (!password.Any(char.IsLower))
    //        throw new DomainException("Password must contain at least one lowercase letter");
    //    if (!password.Any(char.IsDigit))
    //        throw new DomainException("Password must contain at least one digit");
    //    if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
    //        throw new DomainException("Password must contain at least one special character");
    //}

    private static bool SafeBCryptVerify(string? plain, string? hash)
    {
        if (string.IsNullOrWhiteSpace(plain) || string.IsNullOrWhiteSpace(hash))
            return false;

        if (hash is null || !hash.StartsWith("$2", StringComparison.Ordinal))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(plain, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}

