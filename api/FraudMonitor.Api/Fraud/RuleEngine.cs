using FraudMonitor.Api.Models;

namespace FraudMonitor.Api.Fraud;

public sealed class FraudEngineOptions
{
    public const string Section = "FraudEngine";

    /// <summary>A transaction scoring at or above this opens an alert.</summary>
    public int AlertThreshold { get; set; } = 60;
}

public sealed record FiredRule(Rule Rule, string Detail);

public sealed record ScoreResult(int Score, bool IsFlagged, IReadOnlyList<FiredRule> FiredRules);

/// <summary>
/// Runs every enabled rule against a transaction and sums the weights of the ones that fire.
/// Pure: the caller supplies the history and the rule settings, so this is easy to unit test.
/// </summary>
public sealed class RuleEngine
{
    public const int MaxScore = 100;

    private readonly Dictionary<string, IFraudRule> _rules;

    public RuleEngine(IEnumerable<IFraudRule> rules)
    {
        _rules = rules.ToDictionary(r => r.Code, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> KnownCodes => _rules.Keys;

    public ScoreResult Evaluate(
        Transaction transaction,
        IReadOnlyList<Transaction> history,
        IEnumerable<Rule> ruleSettings,
        int alertThreshold)
    {
        var fired = new List<FiredRule>();

        foreach (var setting in ruleSettings.Where(r => r.IsEnabled).OrderBy(r => r.Id))
        {
            // A rule row with no matching implementation is ignored rather than crashing scoring.
            if (!_rules.TryGetValue(setting.Code, out var rule))
                continue;

            var result = rule.Evaluate(transaction, history, setting.Threshold);
            if (result.Hit)
                fired.Add(new FiredRule(setting, result.Detail ?? setting.Name));
        }

        var score = Math.Clamp(fired.Sum(f => f.Rule.Weight), 0, MaxScore);
        return new ScoreResult(score, score >= alertThreshold, fired);
    }
}
