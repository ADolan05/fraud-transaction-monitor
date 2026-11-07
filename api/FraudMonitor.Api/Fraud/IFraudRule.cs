using FraudMonitor.Api.Models;

namespace FraudMonitor.Api.Fraud;

/// <summary>
/// One fraud check. Implementations are pure functions of their inputs (no database access),
/// so each rule can be unit tested on its own.
/// </summary>
public interface IFraudRule
{
    /// <summary>Matches <see cref="Rule.Code"/> in the database.</summary>
    string Code { get; }

    /// <param name="transaction">The transaction being scored. Its Merchant should be loaded.</param>
    /// <param name="history">Earlier transactions on the same account (any order; rules filter by time themselves).</param>
    /// <param name="threshold">The rule's tunable parameter from the database.</param>
    RuleResult Evaluate(Transaction transaction, IReadOnlyList<Transaction> history, decimal threshold);
}

public readonly record struct RuleResult(bool Hit, string? Detail)
{
    public static readonly RuleResult Miss = new(false, null);
    public static RuleResult Fired(string detail) => new(true, detail);
}
