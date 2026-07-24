namespace EasyMoney.Api.Dtos;

public class DashboardDto
{
    public int TotalMembers { get; set; }
    public int TotalAccounts { get; set; }
    public int ActiveAccounts { get; set; }
    public int TotalLoans { get; set; }
    public int ActiveLoans { get; set; }
    public int TotalTenants { get; set; }
    public int PendingKyc { get; set; }
    public int PendingApprovals { get; set; }
    public decimal TotalCollections { get; set; }
    public decimal TotalLoanAmount { get; set; }
    public int CustomersWithoutAccounts { get; set; }
}