namespace FraudMonitor.Api.Models;

public enum AlertStatus
{
    Open,
    Approved,
    Fraud,
    Escalated,
}

/// <summary>Opened when a transaction's risk score reaches the alert threshold. One per flagged transaction.</summary>
public class Alert
{
    public int Id { get; set; }

    public long TransactionId { get; set; }
    public Transaction? Transaction { get; set; }

    /// <summary>The transaction's risk score at the time the alert was opened.</summary>
    public int Score { get; set; }
    public AlertStatus Status { get; set; } = AlertStatus.Open;

    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public List<AlertReason> Reasons { get; set; } = [];
}

/// <summary>Why an alert fired: one row per rule that hit, with a human-readable explanation.</summary>
public class AlertReason
{
    public int AlertId { get; set; }
    public Alert? Alert { get; set; }

    public int RuleId { get; set; }
    public Rule? Rule { get; set; }

    /// <summary>For example "3 transactions in 4 minutes".</summary>
    public required string Detail { get; set; }
}
