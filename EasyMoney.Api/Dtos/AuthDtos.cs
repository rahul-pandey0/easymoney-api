namespace EasyMoney.Api.Dtos;

public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record LogoutRequest(string RefreshToken);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    long UserId,
    string Email,
    string Role,
    long? TenantId, 
    long? MemberId, 
    string? TenantName 
    );

public record MeResponse(
    long UserId,
    string Email,
    string Role,
    long? TenantId,
    long? MemberId,
    bool IsActive);

public record CreateUserRequest(
    string Email,
    string Password,
    string Role,
    long? TenantId,   // ignored when caller is ORG_ADMIN (forced to caller's tenant)
    long? MemberId);

public record UserDto(
    long UserId,
    long? TenantId,
    long? MemberId,
    string Email,
    string? Name,
    string? TenantName,
    string Role,
    bool IsActive,
    long? CreatedBy,
    DateTime CreatedAt,
    long? AuthorizedBy,
    DateTime? AuthorizedAt);

public record ChangeUserStatusRequest(bool IsActive);
public record ChangeUserRoleRequest(string NewRole);
public record SetTenantStatusRequest(string Status);

// Tenant
public record CreateTenantRequest(
    string Name,
    string? RegistrationNumber,
    string? Address,
    string? Phone,
    string? OrgEmail,
    string? ContactPersonName,
    string? ContactPersonPhone,
    DateOnly? StartDate,
    DateOnly? EffectiveDate, 
    bool AuthorisationRequired,
    bool SmsNotification,
    bool EmailNotification
    );

public record TenantDto(
    long TenantId,
    string Name,
    string? RegistrationNumber,
    string? Address,
    string? Phone,
    string? OrgEmail,
    string? ContactPersonName,
    string? ContactPersonPhone,
    DateOnly? StartDate,
    DateOnly? EffectiveDate,
    string Status,
    DateTime CreatedAt,
    long? CreatedBy,
    long? AuthorizedBy,
    DateTime? AuthorizedAt,
    bool AuthorisationRequired,
    bool SmsNotification,
    bool EmailNotification
);



public record GeneralLedgerDto(
    int ? GlId,
    string? Code, 
    string? Name,  
    string? Description,
    bool? Forbank,
    string? Category,
    bool? IsReported,
    bool? HasTransactions,
    bool? HasGst,
    DateTime? CreatedAt,
    long? CreatedBy,
    DateTime? UpdatedAt,    // Add this
    long? UpdatedBy,
    DateTime? AuthorizedAt,
    long? AuthorizedBy,
    string? Status,
    int? ParentGl,
    string? Type
    );
