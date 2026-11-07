using Bogus;
using FraudMonitor.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FraudMonitor.Api.Data;

/// <summary>
/// Fills an empty database with synthetic customers, accounts and merchants.
/// Uses a fixed seed so every fresh database looks the same, which makes demos repeatable.
/// </summary>
public static class DbSeeder
{
    public const int DefaultCustomerCount = 50;
    public const int DefaultMerchantCount = 200;
    private const int Seed = 20261106;

    // Weighted so ordinary spending dominates and risky categories are rare.
    private static readonly (MerchantCategory Category, float Weight, string Suffix)[] CategoryMix =
    [
        (MerchantCategory.Grocery, 0.18f, "Market"),
        (MerchantCategory.Restaurant, 0.20f, "Grill"),
        (MerchantCategory.GasStation, 0.12f, "Fuel"),
        (MerchantCategory.Retail, 0.14f, "Outfitters"),
        (MerchantCategory.Electronics, 0.07f, "Electronics"),
        (MerchantCategory.Pharmacy, 0.07f, "Pharmacy"),
        (MerchantCategory.Travel, 0.06f, "Travel"),
        (MerchantCategory.Entertainment, 0.07f, "Cinemas"),
        (MerchantCategory.GiftCards, 0.03f, "Gift Cards"),
        (MerchantCategory.CryptoExchange, 0.03f, "Crypto Exchange"),
        (MerchantCategory.WireService, 0.03f, "Money Transfer"),
    ];

    /// <returns>True if data was inserted, false if the database already had customers.</returns>
    public static async Task<bool> SeedAsync(
        FraudDbContext db,
        int customerCount = DefaultCustomerCount,
        int merchantCount = DefaultMerchantCount,
        CancellationToken ct = default)
    {
        if (await db.Customers.AnyAsync(ct))
            return false;

        var faker = new Faker("en_US") { Random = new Randomizer(Seed) };
        var now = DateTime.UtcNow;

        db.Customers.AddRange(Enumerable.Range(0, customerCount).Select(_ => CreateCustomer(faker, now)));
        db.Merchants.AddRange(Enumerable.Range(0, merchantCount).Select(_ => CreateMerchant(faker)));

        await db.SaveChangesAsync(ct);
        return true;
    }

    private static Customer CreateCustomer(Faker f, DateTime now)
    {
        var city = f.PickRandom(UsCities.Home);
        var createdAt = f.Date.Between(now.AddYears(-10), now.AddMonths(-3)).ToUniversalTime();

        var customer = new Customer
        {
            FullName = f.Name.FullName(),
            HomeCity = city.Name,
            HomeState = city.State,
            CreatedAt = createdAt,
        };

        var accountCount = f.Random.Int(1, 3);
        for (var i = 0; i < accountCount; i++)
        {
            customer.Accounts.Add(new Account
            {
                // Everyone has a checking account; extras are savings or a credit card.
                AccountType = i == 0 ? AccountType.Checking : f.PickRandom(AccountType.Savings, AccountType.CreditCard),
                MaskedNumber = MaskedAccountNumber(f),
                Balance = Math.Round(f.Random.Decimal(200m, 15_000m), 2),
                OpenedAt = f.Date.Between(createdAt, now).ToUniversalTime(),
            });
        }

        return customer;
    }

    private static Merchant CreateMerchant(Faker f)
    {
        var (category, _, suffix) = f.Random.WeightedRandom(
            CategoryMix,
            CategoryMix.Select(c => c.Weight).ToArray());

        // Most merchants are local; ~15% are far away so travel-based rules have something to catch.
        var city = f.Random.Bool(0.85f) ? f.PickRandom(UsCities.Home) : f.PickRandom(UsCities.Distant);

        return new Merchant
        {
            Name = $"{f.Name.LastName()} {suffix}",
            Category = category,
            City = city.Name,
            State = city.State,
            // Spread merchants a few miles around the city centre.
            Latitude = Math.Round(city.Latitude + f.Random.Double(-0.05, 0.05), 5),
            Longitude = Math.Round(city.Longitude + f.Random.Double(-0.05, 0.05), 5),
        };
    }

    /// <summary>Synthetic, already-masked account number such as "****4821". No full number exists anywhere.</summary>
    public static string MaskedAccountNumber(Faker f) => $"****{f.Random.Int(0, 9999):D4}";
}
