namespace EasyMoney.Api.Auth;

public class JwtSettings
{
    public string Issuer { get; set; } = "EasyMoney";
    public string Audience { get; set; } = "EasyMoney";
    public string SecretKey { get; set; } = null!;
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 30;
}

public static class Roles
{
    public const string SifinAdmin = "SIFIN_ADMIN";
    public const string SifinOperator = "SIFIN_OPERATOR";
    public const string SifinAuthorizer = "SIFIN_AUTHORIZER";
    public const string OrgAdmin = "ORG_ADMIN";
    public const string OrgOperator = "ORG_OPERATOR";
    public const string OrgAuthorizer = "ORG_AUTHORIZER";
    public const string Member = "MEMBER";
    public const string Auditor = "AUDITOR";

    // Common role groups used in [Authorize(Roles=...)] attributes.
    public const string AnySifin = SifinAdmin + "," + SifinOperator + "," + SifinAuthorizer;
    public const string AnyOrg = OrgAdmin + "," + OrgOperator + "," + OrgAuthorizer;
    public const string SifinAdminsAndAuthorizers = SifinAdmin + "," + SifinAuthorizer;
    public const string OrgAdminsAndAuthorizers = OrgAdmin + "," + OrgAuthorizer;
    public const string OrgOperators = OrgAdmin + "," + OrgOperator;
}
