using FraudMonitor.Api.Data;
using FraudMonitor.Api.Fraud;
using FraudMonitor.Api.Models;
using FraudMonitor.Api.Simulation;
using Microsoft.EntityFrameworkCore;

namespace FraudMonitor.Tests;

/// <summary>
/// End to end: seed the database, run each injected fraud pattern through the real rules engine,
/// and check that the rules we expect actually catch it. This is what the demo relies on.
/// </summary>
public class FraudPatternTests
{
    private static async Task<(FraudDbContext Db, TransactionGenerator Generator)> SetUpAsync(int seed)
    {
        var db = TestData.NewDb();
        await DbSeeder.SeedAsync(db);

        var cities = UsCities.Home.ToDictionary(c => c.Name);
        var accounts = await db.Accounts.Include(a => a.Customer)
            .Select(a => new AccountProfile(a.Id, cities[a.Customer!.HomeCity]))
            .ToListAsync();
        var merchants = await db.Merchants.AsNoTracking().ToListAsync();

        return (db, new TransactionGenerator(accounts, merchants, new Random(seed)));
    }

    /// <summary>Plays a batch into the scorer, starting an hour after the seeded history ends.</summary>
    private static async Task<List<ScoreResult>> PlayAsync(FraudDbContext db, IEnumerable<SimulatedTransaction> items)
    {
        var start = DateTime.UtcNow.AddHours(1);
        var scorer = TransactionScorerTests.NewScorer(db);
        var results = new List<ScoreResult>();
        foreach (var item in items)
            results.Add(await scorer.ScoreAndSaveAsync(item.ToTransaction(start + item.Offset)));
        return results;
    }

    public static TheoryData<FraudPattern, int, string[]> Patterns() => new()
    {
        { FraudPattern.CardTesting, 1, [RuleCodes.CardTesting, RuleCodes.Velocity, RuleCodes.LargeAmount] },
        { FraudPattern.ImpossibleTravel, 2, [RuleCodes.ImpossibleTravel, RuleCodes.LargeAmount] },
        { FraudPattern.Spree, 3, [RuleCodes.Velocity, RuleCodes.LargeAmount] },
        { FraudPattern.CashOut, 4, [RuleCodes.Velocity, RuleCodes.RiskyCategory, RuleCodes.LargeAmount] },
    };

    [Theory]
    [MemberData(nameof(Patterns))]
    public async Task EveryPattern_IsCaughtOnEverySeededAccount(FraudPattern pattern, int seed, string[] expectedRules)
    {
        var (db, generator) = await SetUpAsync(seed);
        await using var _ = db;

        // Run against a spread of accounts, not just one lucky one.
        foreach (var account in generator.Accounts.Where((_, i) => i % 10 == 0))
        {
            var batch = generator.Pattern(pattern, account);
            var results = await PlayAsync(db, batch.Items);

            var final = results[^1];
            Assert.True(final.IsFlagged, $"{pattern} on account {account.AccountId} scored only {final.Score}");
            Assert.Superset(expectedRules.ToHashSet(), final.FiredRules.Select(f => f.Rule.Code).ToHashSet());
        }
    }

    [Fact]
    public async Task NormalTraffic_RarelyRaisesAlerts()
    {
        var (db, generator) = await SetUpAsync(seed: 5);
        await using var _ = db;

        // One normal purchase per account, spaced like the simulator does it (well outside the velocity window).
        var start = DateTime.UtcNow.Date.AddDays(1).AddHours(17); // 12–1 p.m. Eastern tomorrow
        var scorer = TransactionScorerTests.NewScorer(db);
        var flagged = 0;
        foreach (var (account, i) in generator.Accounts.Select((a, i) => (a, i)))
        {
            for (var round = 0; round < 3; round++)
            {
                var tx = generator.Normal(account).ToTransaction(start.AddMinutes(round * 4).AddSeconds(i));
                if ((await scorer.ScoreAndSaveAsync(tx)).IsFlagged)
                    flagged++;
            }
        }

        Assert.Equal(0, flagged);
        Assert.Equal(0, await db.Alerts.CountAsync());
    }
}
