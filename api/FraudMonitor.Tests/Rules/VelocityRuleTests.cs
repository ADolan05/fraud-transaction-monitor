using FraudMonitor.Api.Fraud.Rules;
using static FraudMonitor.Tests.TestData;

namespace FraudMonitor.Tests.Rules;

public class VelocityRuleTests
{
    private readonly VelocityRule _rule = new();

    [Fact]
    public void Hits_OnThirdTransactionWithinFiveMinutes()
    {
        var history = new[] { Tx(at: Noon.AddMinutes(-4)), Tx(at: Noon.AddMinutes(-2)) };

        var result = _rule.Evaluate(Tx(at: Noon), history, threshold: 3m);

        Assert.True(result.Hit);
        Assert.Equal("3 transactions in 4 minutes", result.Detail);
    }

    [Fact]
    public void Misses_WithOnlyTwoTransactions()
    {
        var history = new[] { Tx(at: Noon.AddMinutes(-1)) };

        Assert.False(_rule.Evaluate(Tx(at: Noon), history, 3m).Hit);
    }

    [Fact]
    public void Misses_WhenEarlierTransactionsAreOutsideTheWindow()
    {
        var history = new[] { Tx(at: Noon.AddMinutes(-6)), Tx(at: Noon.AddMinutes(-2)) };

        Assert.False(_rule.Evaluate(Tx(at: Noon), history, 3m).Hit);
    }

    [Fact]
    public void Misses_WithNoHistory()
    {
        Assert.False(_rule.Evaluate(Tx(), [], threshold: 1m).Hit);
    }

    [Fact]
    public void RespectsTunedThreshold()
    {
        var history = new[] { Tx(at: Noon.AddMinutes(-3)), Tx(at: Noon.AddMinutes(-2)), Tx(at: Noon.AddMinutes(-1)) };

        Assert.False(_rule.Evaluate(Tx(at: Noon), history, threshold: 5m).Hit);
        Assert.True(_rule.Evaluate(Tx(at: Noon), history, threshold: 4m).Hit);
    }
}
