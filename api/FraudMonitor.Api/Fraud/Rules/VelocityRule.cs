using FraudMonitor.Api.Models;

namespace FraudMonitor.Api.Fraud.Rules;

/// <summary>Fires when [threshold] or more transactions (including this one) hit one account within 5 minutes.</summary>
public sealed class VelocityRule : IFraudRule
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    public string Code => RuleCodes.Velocity;

    public RuleResult Evaluate(Transaction transaction, IReadOnlyList<Transaction> history, decimal threshold)
    {
        var windowStart = transaction.Timestamp - Window;
        var recent = history
            .Where(t => t.Timestamp >= windowStart && t.Timestamp <= transaction.Timestamp)
            .ToList();

        var count = recent.Count + 1;
        if (recent.Count == 0 || count < threshold)
            return RuleResult.Miss;

        var span = transaction.Timestamp - recent.Min(t => t.Timestamp);
        return RuleResult.Fired($"{count} transactions in {Fmt.Duration(span)}");
    }
}
