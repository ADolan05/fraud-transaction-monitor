namespace FraudMonitor.Api.Models;

public enum AccountType
{
    Checking,
    Savings,
    CreditCard,
}

public class Account
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public AccountType AccountType { get; set; }

    /// <summary>Only a masked number is ever stored, e.g. "****4821".</summary>
    public required string MaskedNumber { get; set; }

    public decimal Balance { get; set; }
    public DateTime OpenedAt { get; set; }

    public List<Transaction> Transactions { get; set; } = [];
}
