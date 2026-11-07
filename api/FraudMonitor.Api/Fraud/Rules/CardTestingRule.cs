using FraudMonitor.Api.Models;

namespace FraudMonitor.Api.Fraud.Rules;

/// <summary>
/// Fires when [threshold] or more tiny charges (under $2) in the past hour are followed by a
/// large one. Fraudsters test stolen card numbers with small charges before spending big.
/// </summary>
public sealed class CardTestingRule : IFraudRule
{
    public static readonly TimeSpan Lookback = TimeSpan.FromHours(1);
    public const decimal SmallChargeLimit = 2m;
    public const decimal LargeChargeMinimum = 100m;

    public string Code => RuleCodes.CardTesting;

    public RuleResult Evaluate(Transaction transaction, IReadOnlyList<Transaction> history, decimal threshold)
    {
        if (transaction.Amount < LargeChargeMinimum)
            return RuleResult.Miss;

        var since = transaction.Timestamp - Lookback;
        var small = history
            .Where(t => t.Timestamp >= since && t.Timestamp <= transaction.Timestamp && t.Amount < SmallChargeLimit)
            .ToList();

        if (small.Count == 0 || small.Count < threshold)
            return RuleResult.Miss;

        var span = transaction.Timestamp - small.Min(t => t.Timestamp);
        return RuleResult.Fired(
            $"{small.Count} charges under {Fmt.Money(SmallChargeLimit)} in the {Fmt.Duration(span)} before a {Fmt.Money(transaction.Amount)} charge");
    }
}
