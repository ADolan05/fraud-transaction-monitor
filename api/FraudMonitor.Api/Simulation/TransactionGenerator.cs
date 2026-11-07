using FraudMonitor.Api.Data;
using FraudMonitor.Api.Fraud.Rules;
using FraudMonitor.Api.Models;

namespace FraudMonitor.Api.Simulation;

public sealed record AccountProfile(int AccountId, City Home);

/// <summary>A transaction to be created <see cref="Offset"/> after its batch starts.</summary>
public sealed record SimulatedTransaction(
    TimeSpan Offset,
    int AccountId,
    int MerchantId,
    decimal Amount,
    TransactionChannel Channel,
    double Latitude,
    double Longitude)
{
    public Transaction ToTransaction(DateTime timestamp) => new()
    {
        AccountId = AccountId,
        MerchantId = MerchantId,
        Amount = Amount,
        Currency = "USD",
        Channel = Channel,
        Latitude = Latitude,
        Longitude = Longitude,
        Timestamp = timestamp,
    };
}

/// <summary>Known fraud patterns the simulator injects on purpose, so the demo shows rules catching them.</summary>
public enum FraudPattern
{
    /// <summary>Several sub-$2 charges, then a big online purchase. Card testing + velocity + large amount.</summary>
    CardTesting,
    /// <summary>A purchase at home, then a big in-store purchase 1,000+ miles away 30 s later. Impossible travel + large amount.</summary>
    ImpossibleTravel,
    /// <summary>Four escalating electronics/retail purchases in 90 s. Velocity + large amount.</summary>
    Spree,
    /// <summary>Three escalating gift card / crypto / wire purchases in 80 s. Velocity + risky category + large amount.</summary>
    CashOut,
}

public sealed record GeneratedBatch(FraudPattern? Pattern, IReadOnlyList<SimulatedTransaction> Items, TimeSpan AccountCooldown);

/// <summary>
/// Builds simulated transactions. Normal traffic stays in the customer's home city (or online,
/// located at home) so it doesn't trip the location rules by accident. Takes a Random so tests are repeatable.
/// </summary>
public sealed class TransactionGenerator
{
    /// <summary>Normal traffic leaves an account alone this long, so random traffic can't reach 3 in 5 minutes.</summary>
    public static readonly TimeSpan NormalCooldown = TimeSpan.FromMinutes(3);

    /// <summary>Fraud patterns rest the account longer; after impossible travel, long enough that the trip home looks plausible.</summary>
    public static readonly TimeSpan PatternCooldown = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan TravelCooldown = TimeSpan.FromHours(3);

    private readonly Random _random;
    private readonly Dictionary<string, Merchant[]> _everydayByCity;
    private readonly Dictionary<string, Merchant[]> _spreeByCity;
    private readonly Merchant[] _online;
    private readonly Merchant[] _onlineElectronics;
    private readonly Merchant[] _distant;
    private readonly Merchant[] _risky;

    public IReadOnlyList<AccountProfile> Accounts { get; }

    public TransactionGenerator(IReadOnlyList<AccountProfile> accounts, IReadOnlyList<Merchant> merchants, Random random)
    {
        if (accounts.Count == 0) throw new ArgumentException("No accounts to simulate.", nameof(accounts));
        if (merchants.Count == 0) throw new ArgumentException("No merchants to simulate.", nameof(merchants));

        Accounts = accounts;
        _random = random;

        var distantCities = UsCities.Distant.Select(c => c.Name).ToHashSet();

        _everydayByCity = merchants
            .Where(m => SpendingProfile.Everyday.Contains(m.Category))
            .GroupBy(m => m.City)
            .ToDictionary(g => g.Key, g => g.ToArray());
        _spreeByCity = merchants
            .Where(m => m.Category is MerchantCategory.Electronics or MerchantCategory.Retail)
            .GroupBy(m => m.City)
            .ToDictionary(g => g.Key, g => g.ToArray());
        _online = OrAll(merchants.Where(m => SpendingProfile.Online.Contains(m.Category)), merchants);
        _onlineElectronics = OrAll(merchants.Where(m => m.Category is MerchantCategory.Electronics), _online);
        _distant = merchants.Where(m => distantCities.Contains(m.City)).ToArray();
        _risky = merchants.Where(m => RiskyCategoryRule.IsRisky(m.Category)).ToArray();
    }

    private static Merchant[] OrAll(IEnumerable<Merchant> preferred, IEnumerable<Merchant> fallback)
    {
        var list = preferred.ToArray();
        return list.Length > 0 ? list : fallback.ToArray();
    }

    public bool CanGenerate(FraudPattern pattern) => pattern switch
    {
        FraudPattern.ImpossibleTravel => _distant.Length > 0,
        FraudPattern.CashOut => _risky.Length > 0,
        _ => true,
    };

    public FraudPattern RandomPattern()
    {
        var available = Enum.GetValues<FraudPattern>().Where(CanGenerate).ToArray();
        return available[_random.Next(available.Length)];
    }

    /// <summary>One ordinary purchase: in-store near home, or online.</summary>
    public SimulatedTransaction Normal(AccountProfile account)
    {
        if (_random.NextDouble() < 0.75 && _everydayByCity.TryGetValue(account.Home.Name, out var local))
        {
            var merchant = Pick(local);
            return InStore(TimeSpan.Zero, account, merchant, SpendingProfile.Amount(merchant.Category, _random));
        }

        var online = Pick(_online);
        return Online(TimeSpan.Zero, account, online, SpendingProfile.Amount(online.Category, _random));
    }

    public GeneratedBatch Pattern(FraudPattern pattern, AccountProfile account) => pattern switch
    {
        FraudPattern.CardTesting => CardTesting(account),
        FraudPattern.ImpossibleTravel => ImpossibleTravel(account),
        FraudPattern.Spree => Spree(account),
        FraudPattern.CashOut => CashOut(account),
        _ => throw new ArgumentOutOfRangeException(nameof(pattern)),
    };

    private GeneratedBatch CardTesting(AccountProfile account)
    {
        var items = new List<SimulatedTransaction>();
        for (var i = 0; i < 4; i++)
            items.Add(Online(TimeSpan.FromSeconds(i * 8), account, Pick(_online), Money(0.50m, 1.99m)));

        items.Add(Online(TimeSpan.FromSeconds(40), account, Pick(_onlineElectronics), Money(900m, 1_800m)));
        return new GeneratedBatch(FraudPattern.CardTesting, items, PatternCooldown);
    }

    private GeneratedBatch ImpossibleTravel(AccountProfile account)
    {
        var home = HomeMerchant(account, _everydayByCity);
        var far = Pick(_distant);
        return new GeneratedBatch(
            FraudPattern.ImpossibleTravel,
            [
                home is null
                    ? Online(TimeSpan.Zero, account, Pick(_online), Money(15m, 80m))
                    : InStore(TimeSpan.Zero, account, home, Money(15m, 80m)),
                InStore(TimeSpan.FromSeconds(30), account, far, Money(900m, 2_000m)),
            ],
            TravelCooldown);
    }

    private GeneratedBatch Spree(AccountProfile account)
    {
        // Amounts escalate so later charges still clear 5× an average that the earlier ones have pushed up.
        var start = Money(1_200m, 1_800m);
        decimal[] multipliers = [1m, 1.3m, 1.7m, 2.2m];
        var items = multipliers.Select((m, i) =>
        {
            var offset = TimeSpan.FromSeconds(i * 30);
            var amount = Math.Round(start * m, 2);
            return HomeMerchant(account, _spreeByCity) is { } local
                ? InStore(offset, account, local, amount)
                : Online(offset, account, Pick(_onlineElectronics), amount);
        }).ToList();

        return new GeneratedBatch(FraudPattern.Spree, items, PatternCooldown);
    }

    private GeneratedBatch CashOut(AccountProfile account)
    {
        var start = Money(900m, 1_400m);
        decimal[] multipliers = [1m, 1.6m, 2.5m];
        var items = multipliers
            .Select((m, i) => Online(TimeSpan.FromSeconds(i * 40), account, Pick(_risky), Math.Round(start * m, 2)))
            .ToList();

        return new GeneratedBatch(FraudPattern.CashOut, items, PatternCooldown);
    }

    private Merchant? HomeMerchant(AccountProfile account, Dictionary<string, Merchant[]> byCity) =>
        byCity.TryGetValue(account.Home.Name, out var list) ? Pick(list) : null;

    private static SimulatedTransaction InStore(TimeSpan offset, AccountProfile account, Merchant merchant, decimal amount) =>
        new(offset, account.AccountId, merchant.Id, amount, TransactionChannel.InStore, merchant.Latitude, merchant.Longitude);

    // Online purchases are located where the cardholder is (their home city), as a bank would see from the device IP.
    private static SimulatedTransaction Online(TimeSpan offset, AccountProfile account, Merchant merchant, decimal amount) =>
        new(offset, account.AccountId, merchant.Id, amount, TransactionChannel.Online, account.Home.Latitude, account.Home.Longitude);

    private T Pick<T>(IReadOnlyList<T> items) => items[_random.Next(items.Count)];

    private decimal Money(decimal min, decimal max) => Math.Round(min + (decimal)_random.NextDouble() * (max - min), 2);
}
