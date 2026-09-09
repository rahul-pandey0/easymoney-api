namespace EasyMoney.Api.Auth;

public interface ITenantContext
{
    long? TenantId { get; }
    long? UserId { get; }
    string? Role { get; }
    bool IsSifin { get; }
    bool BypassTenantFilter { get; }
    void SetBypass(bool value);
    long? BranchId { get; set; } 
    string? BranchName { get; set; } 

    DateOnly PreviousDate { get; set; }  
    DateOnly CurrentDate { get; set; }

    DateOnly NextDate { get; set; }
}

public class TenantContext : ITenantContext
{
    public long? TenantId { get; set; }
    public long? UserId { get; set; }
    public string? Role { get; set; }
    public bool IsSifin =>
        Role is "SIFIN_ADMIN" or "SIFIN_OPERATOR" or "SIFIN_AUTHORIZER";
    public bool BypassTenantFilter { get; private set; }
    public void SetBypass(bool value) => BypassTenantFilter = value;
    public long? BranchId { get; set; } 
    public string? BranchName { get; set; }
    public   DateOnly PreviousDate { get; set; }
    public DateOnly CurrentDate { get; set;    }
    public DateOnly NextDate { get; set; }

}
