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
    string? TenantName,
    long ? BranchId,
    string? BranchName,
    DateOnly? previousDate,
    DateOnly? CurrentDate,
    DateOnly? Nextdate


    );
public record BranchsDto(
    long BranchId,
    long TenantId,
    string BranchCode,
    string BranchName,
    string? PreviousDate,
    string? CurrentDate,
    string? NextDate,
    string Status,
    string Address,
    string? PhoneNumber,
    string? Email
);
public record MeResponse(
    long UserId,
    string Email,
    string Role,
    long? TenantId,
    long? MemberId,
    bool IsActive,
    long? Branchid,
        string? BranchName,
    DateOnly? previousDate,
    DateOnly? CurrentDate,
    DateOnly? Nextdate
    );

public record CreateUserRequest(
    string Email,
    string Password,
    string Role,
    long? TenantId,   // ignored when caller is ORG_ADMIN (forced to caller's tenant)
    long? MemberId,
    long? BranchId,
    string? HintQuestion,
    string? HintAnswer,
    string UserName



    );

public record UserDto(
    long UserId,
    long? TenantId,
    long? MemberId,
    string Email,
    string? Name,
    string? UserName,
    long? BranchId,
    string? BranchName,
    string? TenantName,
    string Role,
    bool IsActive,
    long? CreatedBy,
    DateTime CreatedAt,
    long? AuthorizedBy,
    DateTime? AuthorizedAt,
    string? HintQuestion,
    string? HintAnswer
    );

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
    bool EmailNotification,
     string? Logo,  // Base64 encoded image
    string? LogoFileName // For multipart/form-data upload

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
    bool EmailNotification,
    string? LogoBase64,  // Base64 encoded image for display
    string? LogoContentType,
    string? LogoFileName,
    string? LogoUrl  // URL to fetch the logo
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



public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);

public record ResetPasswordRequest(
    long UserId,
    string NewPassword
);

public record ChangePasswordResponse(
    string Message
);

public record VerifyUserRequest(
    string Identifier,  
    string? HintQuestion, 
    string? HintAnswer
);

public record VerifyUserResponse(
    string ResetToken,   
    DateTime ExpiresAt
);

public record ForgotPasswordResetRequest(
    string ResetToken,
    string NewPassword,
    string RetypePassword
);


public record HintQuestionRequest(string Identifier);          // email OR username
public record HintQuestionResponse(string HintQuestion);


public record ForgotPasswordRequest(
    string Identifier,
    string ResetToken,
    string NewPassword,
    string RetypePassword);

public record ForgotPasswordResponse(string Message);