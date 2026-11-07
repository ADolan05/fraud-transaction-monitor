using FraudMonitor.Api.Models;

namespace FraudMonitor.Api.Fraud.Rules;

/// <summary>Fires for gift card, crypto exchange or wire service purchases above $[threshold].</summary>
public sealed class RiskyCategoryRule : IFraudRule
{
    private static readonly Dictionary<MerchantCategory, string> RiskyCategories = new()
    {
        [MerchantCategory.GiftCards] = "gift card",
        [MerchantCategory.CryptoExchange] = "crypto exchange",
        [MerchantCategory.WireService] = "wire service",
    };

    public string Code => RuleCodes.RiskyCategory;

    public static bool IsRisky(MerchantCategory category) => RiskyCategories.ContainsKey(category);

    public RuleResult Evaluate(Transaction transaction, IReadOnlyList<Transaction> history, decimal threshold)
    {
        if (transaction.Merchant is not { } merchant
            || !RiskyCategories.TryGetValue(merchant.Category, out var label)
            || transaction.Amount <= threshold)
        {
            return RuleResult.Miss;
        }

        return RuleResult.Fired($"{Fmt.Money(transaction.Amount)} at a {label} merchant (limit {Fmt.Money(threshold)})");
    }
}
