using FraudMonitor.Api.Fraud.Rules;
using static FraudMonitor.Tests.TestData;

namespace FraudMonitor.Tests.Rules;

public class LargeAmountRuleTests
{
    private readonly LargeAmountRule _rule = new();

    [Fact]
    public void Hits_WhenAmountExceedsMultipleOfAverage()
    {
        var result = _rule.Evaluate(Tx(600m), DailyHistory(10, 50m), threshold: 5m);

        Assert.True(result.Hit);
        Assert.Equal("$600.00 is 12× the 30-day average of $50.00", result.Detail);
    }

    [Fact]
    public void Misses_WhenAmountIsExactlyAtTheLimit()
    {
        var result = _rule.Evaluate(Tx(250m), DailyHistory(10, 50m), threshold: 5m);

        Assert.False(result.Hit);
    }

    [Fact]
    public void Misses_ForOrdinaryAmount()
    {
        Assert.False(_rule.Evaluate(Tx(80m), DailyHistory(10, 50m), 5m).Hit);
    }

    [Fact]
    public void Misses_WithoutEnoughHistoryForABaseline()
    {
        var history = DailyHistory(LargeAmountRule.MinimumHistory - 1, 10m);

        Assert.False(_rule.Evaluate(Tx(5_000m), history, 5m).Hit);
    }

    [Fact]
    public void IgnoresTransactionsOlderThan30Days()
    {
        // Ten old, expensive purchases would raise the average if they counted.
        var history = DailyHistory(10, 50m)
            .Concat(DailyHistory(10, 10_000m, before: Noon.AddDays(-31)))
            .ToList();

        Assert.True(_rule.Evaluate(Tx(600m), history, 5m).Hit);
    }
}
