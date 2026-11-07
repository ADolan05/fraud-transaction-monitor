using FraudMonitor.Api.Fraud;
using FraudMonitor.Api.Models;
using static FraudMonitor.Tests.TestData;

namespace FraudMonitor.Tests;

public class RuleEngineTests
{
    /// <summary>A rule whose outcome the test controls.</summary>
    private sealed class FakeRule(string code, bool hit) : IFraudRule
    {
        public string Code => code;
        public RuleResult Evaluate(Transaction transaction, IReadOnlyList<Transaction> history, decimal threshold) =>
            hit ? RuleResult.Fired($"{code} fired") : RuleResult.Miss;
    }

    private static Rule Setting(int id, string code, int weight, bool enabled = true) =>
        new() { Id = id, Code = code, Name = code, Description = "", Weight = weight, IsEnabled = enabled };

    [Fact]
    public void SumsWeightsOfRulesThatFire()
    {
        var engine = new RuleEngine([new FakeRule("A", true), new FakeRule("B", true), new FakeRule("C", false)]);
        Rule[] settings = [Setting(1, "A", 30), Setting(2, "B", 35), Setting(3, "C", 45)];

        var result = engine.Evaluate(Tx(), [], settings, alertThreshold: 60);

        Assert.Equal(65, result.Score);
        Assert.True(result.IsFlagged);
        Assert.Equal(["A", "B"], result.FiredRules.Select(f => f.Rule.Code));
        Assert.Equal("A fired", result.FiredRules[0].Detail);
    }

    [Fact]
    public void FlagsExactlyAtThreshold_AndNotBelow()
    {
        var engine = new RuleEngine([new FakeRule("A", true)]);

        Assert.True(engine.Evaluate(Tx(), [], [Setting(1, "A", 60)], 60).IsFlagged);
        Assert.False(engine.Evaluate(Tx(), [], [Setting(1, "A", 59)], 60).IsFlagged);
    }

    [Fact]
    public void CapsScoreAt100()
    {
        var engine = new RuleEngine([new FakeRule("A", true), new FakeRule("B", true), new FakeRule("C", true)]);
        Rule[] settings = [Setting(1, "A", 45), Setting(2, "B", 40), Setting(3, "C", 35)];

        Assert.Equal(100, engine.Evaluate(Tx(), [], settings, 60).Score);
    }

    [Fact]
    public void SkipsDisabledRules()
    {
        var engine = new RuleEngine([new FakeRule("A", true), new FakeRule("B", true)]);
        Rule[] settings = [Setting(1, "A", 30), Setting(2, "B", 35, enabled: false)];

        var result = engine.Evaluate(Tx(), [], settings, 60);

        Assert.Equal(30, result.Score);
        Assert.False(result.IsFlagged);
        Assert.Single(result.FiredRules);
    }

    [Fact]
    public void IgnoresSettingsWithNoImplementation()
    {
        var engine = new RuleEngine([new FakeRule("A", true)]);
        Rule[] settings = [Setting(1, "A", 30), Setting(2, "UNKNOWN", 50)];

        Assert.Equal(30, engine.Evaluate(Tx(), [], settings, 60).Score);
    }

    [Fact]
    public void DefaultRules_AllHaveAnImplementation()
    {
        var engine = new RuleEngine(
        [
            new Api.Fraud.Rules.LargeAmountRule(), new Api.Fraud.Rules.VelocityRule(),
            new Api.Fraud.Rules.ImpossibleTravelRule(), new Api.Fraud.Rules.OddHourRule(),
            new Api.Fraud.Rules.RiskyCategoryRule(), new Api.Fraud.Rules.CardTestingRule(),
        ]);

        Assert.Equal(
            DefaultRules.All.Select(r => r.Code).Order(),
            engine.KnownCodes.Order());
    }
}
