using Bogus;
using FraudMonitor.Api.Fraud.Rules;
using FraudMonitor.Api.Models;
using FraudMonitor.Api.Simulation;
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

    /// <summary>Each account gets this many past transactions, so the rules have a baseline to compare against.</summary>
    public const int MinHistoryPerAccount = 25;
    public const int MaxHistoryPerAccount = 45;
    public static readonly TimeSpan HistoryWindow = TimeSpan.FromDays(30);

    /// <returns>True if any data was inserted.</returns>
    public static async Task<bool> SeedAsync(
        FraudDbContext db,
        int customerCount = DefaultCustomerCount,
        int merchantCount = DefaultMerchantCount,
        CancellationToken ct = default)
    {
        var faker = new Faker("en_US") { Random = new Randomizer(Seed) };
        var now = DateTime.UtcNow;
        var seeded = false;

        if (!await db.Customers.AnyAsync(ct))
        {
            db.Customers.AddRange(Enumerable.Range(0, customerCount).Select(_ => CreateCustomer(faker, now)));
            db.Merchants.AddRange(Enumerable.Range(0, merchantCount).Select(_ => CreateMerchant(faker)));
            await db.SaveChangesAsync(ct);
            seeded = true;
        }

        // Checked separately so databases seeded before history existed get it too.
        if (!await db.Transactions.AnyAsync(ct))
        {
            await SeedHistoryAsync(db, faker, now, ct);
            seeded = true;
        }

        return seeded;
    }

    /// <summary>
    /// 30 days of ordinary spending per account: daytime, home city or online, everyday categories.
    /// Saved as already Cleared; this is the "normal" the rules measure new transactions against.
    /// </summary>
    private static async Task SeedHistoryAsync(FraudDbContext db, Faker f, DateTime now, CancellationToken ct)
    {
        var accounts = await db.Accounts
            .Select(a => new { a.Id, a.OpenedAt, a.Customer!.HomeCity })
            .ToListAsync(ct);
        var merchants = await db.Merchants.ToListAsync(ct);
        if (accounts.Count == 0 || merchants.Count == 0)
            return;

        var cities = UsCities.Home.ToDictionary(c => c.Name);
        var everyday = merchants.Where(m => SpendingProfile.Everyday.Contains(m.Category)).ToList();
        var online = merchants.Where(m => SpendingProfile.Online.Contains(m.Category) && !RiskyCategoryRule.IsRisky(m.Category)).ToList();
        var random = new Random(Seed);

        foreach (var account in accounts)
        {
            if (!cities.TryGetValue(account.HomeCity, out var home))
                continue;

            var local = everyday.Where(m => m.City == home.Name).ToList();
            var count = f.Random.Int(MinHistoryPerAccount, MaxHistoryPerAccount);

            for (var i = 0; i < count; i++)
            {
                // Between 8 a.m. and 10 p.m. Eastern (UTC-5), on a random day in the last 30 days.
                var day = now.Date.AddDays(-f.Random.Int(2, (int)HistoryWindow.TotalDays));
                var timestamp = DateTime.SpecifyKind(
                    day.AddHours(13).AddMinutes(f.Random.Int(0, 14 * 60 - 1)).AddSeconds(f.Random.Int(0, 59)),
                    DateTimeKind.Utc);

                var inStore = local.Count > 0 && f.Random.Bool(0.8f);
                var merchant = inStore ? f.PickRandom(local) : f.PickRandom(online.Count > 0 ? online : everyday);

                db.Transactions.Add(new Transaction
                {
                    AccountId = account.Id,
                    MerchantId = merchant.Id,
                    Amount = SpendingProfile.Amount(merchant.Category, random),
                    Currency = "USD",
                    Timestamp = timestamp,
                    Channel = inStore ? TransactionChannel.InStore : TransactionChannel.Online,
                    Latitude = inStore ? merchant.Latitude : home.Latitude,
                    Longitude = inStore ? merchant.Longitude : home.Longitude,
                    RiskScore = 0,
                    Status = TransactionStatus.Cleared,
                });
            }
        }

        await db.SaveChangesAsync(ct);
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
