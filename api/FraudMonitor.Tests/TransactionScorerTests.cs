using FraudMonitor.Api.Data;
using FraudMonitor.Api.Fraud;
using FraudMonitor.Api.Fraud.Rules;
using FraudMonitor.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FraudMonitor.Tests;

public class TransactionScorerTests
{
    private static readonly IFraudRule[] AllRules =
    [
        new LargeAmountRule(), new VelocityRule(), new ImpossibleTravelRule(),
        new OddHourRule(), new RiskyCategoryRule(), new CardTestingRule(),
    ];

    internal static TransactionScorer NewScorer(FraudDbContext db) =>
        new(db, new RuleEngine(AllRules), Options.Create(new FraudEngineOptions { AlertThreshold = 60 }));

    private static async Task<(Account Account, Merchant Home, Merchant Miami)> SeedAccountAsync(FraudDbContext db)
    {
        var customer = new Customer { FullName = "Test Person", HomeCity = "Buffalo", HomeState = "NY", CreatedAt = TestData.Noon.AddYears(-1) };
        var account = new Account { Customer = customer, MaskedNumber = "****0001", OpenedAt = TestData.Noon.AddYears(-1) };
        var home = new Merchant { Name = "Home Market", Category = MerchantCategory.Grocery, City = "Buffalo", State = "NY", Latitude = TestData.Buffalo.Latitude, Longitude = TestData.Buffalo.Longitude };
        var miami = new Merchant { Name = "Miami Electronics", Category = MerchantCategory.Electronics, City = "Miami", State = "FL", Latitude = TestData.Miami.Latitude, Longitude = TestData.Miami.Longitude };
        db.AddRange(account, home, miami);
        await db.SaveChangesAsync();

        // Ten days of ordinary $50 purchases.
        for (var i = 1; i <= 10; i++)
        {
            db.Transactions.Add(new Transaction
            {
                AccountId = account.Id, MerchantId = home.Id, Amount = 50m, Timestamp = TestData.Noon.AddDays(-i),
                Latitude = home.Latitude, Longitude = home.Longitude, Status = TransactionStatus.Cleared,
            });
        }
        await db.SaveChangesAsync();
        return (account, home, miami);
    }

    private static Transaction At(Account account, Merchant merchant, decimal amount, DateTime timestamp) => new()
    {
        AccountId = account.Id, MerchantId = merchant.Id, Amount = amount, Timestamp = timestamp,
        Latitude = merchant.Latitude, Longitude = merchant.Longitude,
    };

    [Fact]
    public async Task OrdinaryTransaction_IsClearedWithoutAlert()
    {
        await using var db = TestData.NewDb();
        var (account, home, _) = await SeedAccountAsync(db);

        var tx = At(account, home, 45m, TestData.Noon);
        var result = await NewScorer(db).ScoreAndSaveAsync(tx);

        Assert.Equal(0, result.Score);
        Assert.Equal(TransactionStatus.Cleared, tx.Status);
        Assert.Equal(0, await db.Alerts.CountAsync());
    }

    [Fact]
    public async Task SuspiciousTransaction_OpensAlertWithReasons()
    {
        await using var db = TestData.NewDb();
        var (account, home, miami) = await SeedAccountAsync(db);
        var scorer = NewScorer(db);

        await scorer.ScoreAndSaveAsync(At(account, home, 40m, TestData.Noon));
        var big = At(account, miami, 1_500m, TestData.Noon.AddMinutes(10));
        var result = await scorer.ScoreAndSaveAsync(big);

        // Impossible travel (45) + large amount (30).
        Assert.Equal(75, result.Score);
        Assert.Equal(TransactionStatus.Flagged, big.Status);
        Assert.Equal(75, big.RiskScore);

        var alert = await db.Alerts.Include(a => a.Reasons).ThenInclude(r => r.Rule).SingleAsync();
        Assert.Equal(big.Id, alert.TransactionId);
        Assert.Equal(AlertStatus.Open, alert.Status);
        Assert.Equal(75, alert.Score);
        Assert.Equal(
            [RuleCodes.ImpossibleTravel, RuleCodes.LargeAmount],
            alert.Reasons.Select(r => r.Rule!.Code).Order());
    }

    [Fact]
    public async Task DisabledRule_DoesNotContribute()
    {
        await using var db = TestData.NewDb();
        var (account, home, miami) = await SeedAccountAsync(db);
        var travel = await db.Rules.SingleAsync(r => r.Code == RuleCodes.ImpossibleTravel);
        travel.IsEnabled = false;
        await db.SaveChangesAsync();
        var scorer = NewScorer(db);

        await scorer.ScoreAndSaveAsync(At(account, home, 40m, TestData.Noon));
        var result = await scorer.ScoreAndSaveAsync(At(account, miami, 1_500m, TestData.Noon.AddMinutes(10)));

        Assert.Equal(30, result.Score);
        Assert.False(result.IsFlagged);
        Assert.Equal(0, await db.Alerts.CountAsync());
    }

    [Fact]
    public async Task TunedWeight_ChangesScoreWithoutRedeploy()
    {
        await using var db = TestData.NewDb();
        var (account, home, _) = await SeedAccountAsync(db);
        var large = await db.Rules.SingleAsync(r => r.Code == RuleCodes.LargeAmount);
        large.Weight = 70;
        await db.SaveChangesAsync();

        var result = await NewScorer(db).ScoreAndSaveAsync(At(account, home, 1_000m, TestData.Noon));

        Assert.Equal(70, result.Score);
        Assert.True(result.IsFlagged);
    }
}
