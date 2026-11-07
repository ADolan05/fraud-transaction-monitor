using FraudMonitor.Api.Fraud.Rules;
using static FraudMonitor.Tests.TestData;

namespace FraudMonitor.Tests.Rules;

public class CardTestingRuleTests
{
    private readonly CardTestingRule _rule = new();

    private static List<Api.Models.Transaction> SmallCharges(int count, decimal amount = 1.00m) =>
        Enumerable.Range(1, count).Select(i => Tx(amount, Noon.AddMinutes(-i * 2))).ToList();

    [Fact]
    public void Hits_SmallChargesFollowedByLargeOne()
    {
        var result = _rule.Evaluate(Tx(900m), SmallCharges(3), threshold: 3m);

        Assert.True(result.Hit);
        Assert.Equal("3 charges under $2.00 in the 6 minutes before a $900.00 charge", result.Detail);
    }

    [Fact]
    public void Misses_WhenFinalChargeIsSmall()
    {
        Assert.False(_rule.Evaluate(Tx(40m), SmallCharges(5), 3m).Hit);
    }

    [Fact]
    public void Misses_WithTooFewSmallCharges()
    {
        Assert.False(_rule.Evaluate(Tx(900m), SmallCharges(2), 3m).Hit);
    }

    [Fact]
    public void Misses_WhenChargesAreNotUnderTwoDollars()
    {
        Assert.False(_rule.Evaluate(Tx(900m), SmallCharges(5, amount: 2.00m), 3m).Hit);
    }

    [Fact]
    public void Misses_WhenSmallChargesAreOlderThanAnHour()
    {
        var old = Enumerable.Range(0, 5).Select(i => Tx(1m, Noon.AddMinutes(-61 - i))).ToList();

        Assert.False(_rule.Evaluate(Tx(900m), old, 3m).Hit);
    }
}
