using FraudMonitor.Api.Data;
using FraudMonitor.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FraudMonitor.Tests;

/// <summary>Builders shared by the rule and engine tests.</summary>
internal static class TestData
{
    // Midday Eastern (17:00 UTC = 1 p.m. EDT) on a fixed date, so tests don't depend on the clock.
    public static readonly DateTime Noon = new(2026, 3, 10, 17, 0, 0, DateTimeKind.Utc);

    public static readonly City Buffalo = UsCities.Home.Single(c => c.Name == "Buffalo");
    public static readonly City Rochester = UsCities.Home.Single(c => c.Name == "Rochester");
    public static readonly City Miami = UsCities.Distant.Single(c => c.Name == "Miami");

    public static Transaction Tx(
        decimal amount = 50m,
        DateTime? at = null,
        City? city = null,
        MerchantCategory category = MerchantCategory.Grocery)
    {
        var c = city ?? Buffalo;
        return new Transaction
        {
            AccountId = 1,
            MerchantId = 1,
            Merchant = new Merchant { Id = 1, Name = "Test", Category = category, City = c.Name, State = c.State },
            Amount = amount,
            Timestamp = at ?? Noon,
            Latitude = c.Latitude,
            Longitude = c.Longitude,
        };
    }

    /// <summary>Daytime transactions, one per day going back from the reference time.</summary>
    public static List<Transaction> DailyHistory(int count, decimal amount = 50m, DateTime? before = null) =>
        Enumerable.Range(1, count).Select(i => Tx(amount, (before ?? Noon).AddDays(-i))).ToList();

    public static FraudDbContext NewDb()
    {
        var db = new FraudDbContext(new DbContextOptionsBuilder<FraudDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        db.Database.EnsureCreated(); // applies HasData, i.e. the default rules
        return db;
    }
}
