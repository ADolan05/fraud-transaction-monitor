namespace FraudMonitor.Api.Models;

/// <summary>
/// Configuration for one fraud rule. The logic lives in code (an IFraudRule with the same Code);
/// the weight, threshold and on/off switch live here so admins can tune them without a redeploy.
/// </summary>
public class Rule
{
    public int Id { get; set; }

    /// <summary>Stable identifier that links this row to its IFraudRule implementation, e.g. "VELOCITY".</summary>
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }

    /// <summary>Points added to the transaction's risk score when the rule fires.</summary>
    public int Weight { get; set; }

    /// <summary>The rule's tunable parameter; what it means depends on the rule (see Description).</summary>
    public decimal Threshold { get; set; }

    public bool IsEnabled { get; set; } = true;
}
