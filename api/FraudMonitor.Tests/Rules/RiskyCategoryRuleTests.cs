using FraudMonitor.Api.Fraud.Rules;
using FraudMonitor.Api.Models;
using static FraudMonitor.Tests.TestData;

namespace FraudMonitor.Tests.Rules;

public class RiskyCategoryRuleTests
{
    private readonly RiskyCategoryRule _rule = new();

    [Theory]
    [InlineData(MerchantCategory.GiftCards, "gift card")]
    [InlineData(MerchantCategory.CryptoExchange, "crypto exchange")]
    [InlineData(MerchantCategory.WireService, "wire service")]
    public void Hits_RiskyCategoriesAboveLimit(MerchantCategory category, string label)
    {
        var result = _rule.Evaluate(Tx(750m, category: category), [], threshold: 500m);

        Assert.True(result.Hit);
        Assert.Equal($"$750.00 at a {label} merchant (limit $500.00)", result.Detail);
    }

    [Fact]
    public void Misses_RiskyCategoryAtOrBelowLimit()
    {
        Assert.False(_rule.Evaluate(Tx(500m, category: MerchantCategory.GiftCards), [], 500m).Hit);
    }

    [Fact]
    public void Misses_LargePurchaseInOrdinaryCategory()
    {
        Assert.False(_rule.Evaluate(Tx(5_000m, category: MerchantCategory.Electronics), [], 500m).Hit);
    }

    [Fact]
    public void Misses_WhenMerchantNotLoaded()
    {
        var tx = Tx(5_000m, category: MerchantCategory.CryptoExchange);
        tx.Merchant = null;

        Assert.False(_rule.Evaluate(tx, [], 500m).Hit);
    }
}
