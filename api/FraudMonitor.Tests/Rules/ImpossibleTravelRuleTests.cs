using FraudMonitor.Api.Fraud.Rules;
using static FraudMonitor.Tests.TestData;

namespace FraudMonitor.Tests.Rules;

public class ImpossibleTravelRuleTests
{
    private readonly ImpossibleTravelRule _rule = new();

    [Fact]
    public void Hits_BuffaloToMiamiIn20Minutes()
    {
        var history = new[] { Tx(at: Noon.AddMinutes(-20), city: Buffalo) };

        var result = _rule.Evaluate(Tx(at: Noon, city: Miami), history, threshold: 500m);

        Assert.True(result.Hit);
        Assert.Matches(@"^1,1\d\d miles from a transaction 20 minutes earlier \(would need [\d,]+ mph\)$", result.Detail);
    }

    [Fact]
    public void Misses_BuffaloToMiamiOneDayLater()
    {
        var history = new[] { Tx(at: Noon.AddHours(-5), city: Buffalo) };

        // 1,180 miles in 5 hours is ~236 mph: a flight, not fraud.
        Assert.False(_rule.Evaluate(Tx(at: Noon, city: Miami), history, 500m).Hit);
    }

    [Fact]
    public void Misses_ShortHopsEvenIfFast()
    {
        // Buffalo to Rochester is ~65 miles: below the 100-mile minimum, so timing noise can't trigger it.
        var history = new[] { Tx(at: Noon.AddMinutes(-1), city: Buffalo) };

        Assert.False(_rule.Evaluate(Tx(at: Noon, city: Rochester), history, 500m).Hit);
    }

    [Fact]
    public void Misses_SameCity()
    {
        var history = new[] { Tx(at: Noon.AddSeconds(-5), city: Buffalo) };

        Assert.False(_rule.Evaluate(Tx(at: Noon, city: Buffalo), history, 500m).Hit);
    }

    [Fact]
    public void Misses_WhenEarlierTransactionIsOutside24Hours()
    {
        var history = new[] { Tx(at: Noon.AddHours(-25), city: Buffalo) };

        Assert.False(_rule.Evaluate(Tx(at: Noon, city: Miami), history, threshold: 1m).Hit);
    }
}
