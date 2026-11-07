using FraudMonitor.Api.Models;

namespace FraudMonitor.Api.Simulation;

/// <summary>Typical (legitimate) purchase amounts per merchant category.</summary>
public static class SpendingProfile
{
    private static readonly Dictionary<MerchantCategory, (decimal Min, decimal Max)> Ranges = new()
    {
        [MerchantCategory.Grocery] = (12m, 160m),
        [MerchantCategory.Restaurant] = (9m, 95m),
        [MerchantCategory.GasStation] = (20m, 85m),
        [MerchantCategory.Retail] = (10m, 220m),
        [MerchantCategory.Electronics] = (25m, 450m),
        [MerchantCategory.Pharmacy] = (5m, 70m),
        [MerchantCategory.Travel] = (90m, 750m),
        [MerchantCategory.Entertainment] = (10m, 90m),
        [MerchantCategory.GiftCards] = (25m, 100m),
        [MerchantCategory.CryptoExchange] = (50m, 300m),
        [MerchantCategory.WireService] = (50m, 400m),
    };

    /// <summary>Day-to-day categories used for seeded history and most simulated traffic.</summary>
    public static readonly MerchantCategory[] Everyday =
    [
        MerchantCategory.Grocery, MerchantCategory.Restaurant, MerchantCategory.GasStation,
        MerchantCategory.Retail, MerchantCategory.Pharmacy, MerchantCategory.Entertainment,
    ];

    /// <summary>Categories that are commonly bought online.</summary>
    public static readonly MerchantCategory[] Online =
    [
        MerchantCategory.Retail, MerchantCategory.Electronics, MerchantCategory.Travel,
        MerchantCategory.Entertainment, MerchantCategory.GiftCards,
    ];

    public static decimal Amount(MerchantCategory category, Random random)
    {
        var (min, max) = Ranges[category];
        return Math.Round(min + (decimal)random.NextDouble() * (max - min), 2);
    }
}
