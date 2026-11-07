using FraudMonitor.Api.Fraud.Rules;
using static FraudMonitor.Tests.TestData;

namespace FraudMonitor.Tests.Rules;

public class OddHourRuleTests
{
    private readonly OddHourRule _rule = new();

    // 3:00 a.m. Eastern (EDT, UTC-4) on 2026-03-10.
    private static readonly DateTime ThreeAmBuffalo = new(2026, 3, 10, 7, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Hits_At3amForAnAccountThatNeverSpendsAtNight()
    {
        var result = _rule.Evaluate(Tx(at: ThreeAmBuffalo), DailyHistory(20), threshold: 0.10m);

        Assert.True(result.Hit);
        Assert.Equal("3:00 AM local time; 0% of this account's last 30 days was between 1 and 5 AM", result.Detail);
    }

    [Fact]
    public void Misses_AtNoon()
    {
        Assert.False(_rule.Evaluate(Tx(at: Noon), DailyHistory(20), 0.10m).Hit);
    }

    [Fact]
    public void Misses_ForANightOwlAccount()
    {
        // Half of this account's history is at 3 a.m., so a 3 a.m. purchase is normal for them.
        var nights = Enumerable.Range(1, 10).Select(i => Tx(at: ThreeAmBuffalo.AddDays(-i)));
        var history = DailyHistory(10).Concat(nights).ToList();

        Assert.False(_rule.Evaluate(Tx(at: ThreeAmBuffalo), history, 0.10m).Hit);
    }

    [Fact]
    public void UsesLocalTimeAtTheTransactionLocation()
    {
        // 07:00 UTC is 3 a.m. in Buffalo but midnight in Los Angeles.
        var losAngeles = Api.Data.UsCities.Distant.Single(c => c.Name == "Los Angeles");

        Assert.False(_rule.Evaluate(Tx(at: ThreeAmBuffalo, city: losAngeles), [], 0.10m).Hit);
    }

    [Fact]
    public void Hits_WithNoHistory()
    {
        Assert.True(_rule.Evaluate(Tx(at: ThreeAmBuffalo), [], 0.10m).Hit);
    }
}
