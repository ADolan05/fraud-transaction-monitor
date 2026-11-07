namespace FraudMonitor.Api.Models;

public enum TransactionChannel
{
    InStore,
    Online,
    Atm,
}

public enum TransactionStatus
{
    /// <summary>Created but not yet scored by the rules engine.</summary>
    Pending,
    /// <summary>Scored below the alert threshold.</summary>
    Cleared,
    /// <summary>Scored at or above the alert threshold; an alert was opened.</summary>
    Flagged,
}

public class Transaction
{
    public long Id { get; set; }

    public int AccountId { get; set; }
    public Account? Account { get; set; }

    public int MerchantId { get; set; }
    public Merchant? Merchant { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime Timestamp { get; set; }
    public TransactionChannel Channel { get; set; }

    /// <summary>Where the transaction happened (used by the impossible-travel rule).</summary>
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>0–100, filled in by the rules engine.</summary>
    public int RiskScore { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
}
