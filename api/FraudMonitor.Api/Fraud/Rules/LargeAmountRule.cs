using FraudMonitor.Api.Models;

namespace FraudMonitor.Api.Fraud.Rules;

/// <summary>Fires when the amount is more than [threshold]× the account's 30-day average.</summary>
public sealed class LargeAmountRule : IFraudRule
{
    public static readonly TimeSpan Lookback = TimeSpan.FromDays(30);

    /// <summary>Below this many past transactions there is no meaningful average, so the rule stays quiet.</summary>
    public const int MinimumHistory = 5;

    public string Code => RuleCodes.LargeAmount;

    public RuleResult Evaluate(Transaction transaction, IReadOnlyList<Transaction> history, decimal threshold)
    {
        var since = transaction.Timestamp - Lookback;
        var amounts = history
            .Where(t => t.Timestamp >= since && t.Timestamp <= transaction.Timestamp)
            .Select(t => t.Amount)
            .ToList();

        if (amounts.Count < MinimumHistory)
            return RuleResult.Miss;

        var average = amounts.Average();
        if (average <= 0 || transaction.Amount <= average * threshold)
            return RuleResult.Miss;

        var multiple = (double)(transaction.Amount / average);
        return RuleResult.Fired(
            $"{Fmt.Money(transaction.Amount)} is {Fmt.Number(multiple, "0.#")}× the 30-day average of {Fmt.Money(Math.Round(average, 2))}");
    }
}
