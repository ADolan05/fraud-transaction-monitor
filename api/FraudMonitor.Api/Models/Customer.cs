namespace FraudMonitor.Api.Models;

/// <summary>A synthetic bank customer. All data is generated; no real people.</summary>
public class Customer
{
    public int Id { get; set; }
    public required string FullName { get; set; }
    public required string HomeCity { get; set; }
    public required string HomeState { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<Account> Accounts { get; set; } = [];
}
