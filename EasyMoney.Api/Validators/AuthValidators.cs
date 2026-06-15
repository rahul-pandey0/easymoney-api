using EasyMoney.Api.Dtos;
using FluentValidation;

namespace EasyMoney.Api.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
    }
}

public class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    private static readonly string[] AllowedRoles =
    {
        "SIFIN_ADMIN","SIFIN_OPERATOR","SIFIN_AUTHORIZER",
        "ORG_ADMIN","ORG_OPERATOR","ORG_AUTHORIZER",
        "MEMBER","AUDITOR"
    };

    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(150);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.Role).Must(r => AllowedRoles.Contains(r))
            .WithMessage("Role must be one of: " + string.Join(", ", AllowedRoles));
    }
}
