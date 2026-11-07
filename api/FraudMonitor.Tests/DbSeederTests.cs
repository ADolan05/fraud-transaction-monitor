using System.Text.RegularExpressions;
using FraudMonitor.Api.Data;
using FraudMonitor.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FraudMonitor.Tests;

public class DbSeederTests
{
    private static FraudDbContext NewContext() =>
        new(new DbContextOptionsBuilder<FraudDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Seed_CreatesDefaultCustomersAndMerchants()
    {
        await using var db = NewContext();

        var seeded = await DbSeeder.SeedAsync(db);

        Assert.True(seeded);
        Assert.Equal(DbSeeder.DefaultCustomerCount, await db.Customers.CountAsync());
        Assert.Equal(DbSeeder.DefaultMerchantCount, await db.Merchants.CountAsync());
    }

    [Fact]
    public async Task Seed_IsIdempotent()
    {
        await using var db = NewContext();

        await DbSeeder.SeedAsync(db);
        var seededAgain = await DbSeeder.SeedAsync(db);

        Assert.False(seededAgain);
        Assert.Equal(DbSeeder.DefaultCustomerCount, await db.Customers.CountAsync());
    }

    [Fact]
    public async Task Seed_GivesEveryCustomerACheckingAccount()
    {
        await using var db = NewContext();
        await DbSeeder.SeedAsync(db);

        var customers = await db.Customers.Include(c => c.Accounts).ToListAsync();

        Assert.All(customers, c =>
        {
            Assert.InRange(c.Accounts.Count, 1, 3);
            Assert.Contains(c.Accounts, a => a.AccountType == AccountType.Checking);
        });
    }

    [Fact]
    public async Task Seed_StoresOnlyMaskedAccountNumbers()
    {
        await using var db = NewContext();
        await DbSeeder.SeedAsync(db);

        var numbers = await db.Accounts.Select(a => a.MaskedNumber).ToListAsync();

        Assert.NotEmpty(numbers);
        Assert.All(numbers, n => Assert.Matches(new Regex(@"^\*{4}\d{4}$"), n));
    }

    [Fact]
    public async Task Seed_UsesUtcTimestamps()
    {
        await using var db = NewContext();
        await DbSeeder.SeedAsync(db);

        var accounts = await db.Accounts.Include(a => a.Customer).ToListAsync();

        Assert.All(accounts, a =>
        {
            Assert.Equal(DateTimeKind.Utc, a.OpenedAt.Kind);
            Assert.True(a.OpenedAt >= a.Customer!.CreatedAt, "Account opened before its customer existed.");
        });
    }

    [Fact]
    public async Task Seed_IncludesMerchantsInRiskyCategoriesAndDistantCities()
    {
        await using var db = NewContext();
        await DbSeeder.SeedAsync(db);

        var merchants = await db.Merchants.ToListAsync();
        var risky = new[] { MerchantCategory.GiftCards, MerchantCategory.CryptoExchange, MerchantCategory.WireService };
        var distantCities = UsCities.Distant.Select(c => c.Name).ToHashSet();

        Assert.Contains(merchants, m => risky.Contains(m.Category));
        Assert.Contains(merchants, m => distantCities.Contains(m.City));
    }
}
