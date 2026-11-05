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
    public decimal LoanRecoveryPercentage { get; set; }
    public int ApprovedKyc { get; set; }
    public decimal TotalOutstandingBalance { get; set; } 
    public List<DailyCollectionDto> PaymentAmount { get; set; } = new();

    public DateOnly? PreviousDate { get; set; }
    public DateOnly? CurrentDate { get; set; }
    public DateOnly? NextDate { get; set; }
    public DateOnly CutoffDate { get; set; }
    public DateOnly? BiddingDate { get; set; }
    public List<TenantCount> Count { get; set; }
    public int SuspendedTenantcount { get; set; } 
    public decimal PoolMoney { get; set; } 
    public decimal PoolMoneyCollections { get; set; }  
} 
public class DailyCollectionDto
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
}
public class TenantCount
{
    public DateTime Month { get; set; }
    public int Count { get; set; }
}