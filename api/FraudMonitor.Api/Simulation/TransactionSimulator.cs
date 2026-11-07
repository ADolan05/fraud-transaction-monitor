using FraudMonitor.Api.Data;
using FraudMonitor.Api.Fraud;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FraudMonitor.Api.Simulation;

public sealed class SimulatorOptions
{
    public const string Section = "Simulator";

    /// <summary>Whether the stream starts automatically when the API starts.</summary>
    public bool Enabled { get; set; } = true;
    public int MinDelayMs { get; set; } = 2_000;
    public int MaxDelayMs { get; set; } = 5_000;

    /// <summary>Share of new batches that are a known fraud pattern instead of a normal purchase.</summary>
    public double FraudPatternChance { get; set; } = 0.06;
}

/// <summary>On/off switch for the stream. Singleton so the admin start/stop endpoints (week 3) can flip it.</summary>
public sealed class SimulatorState(IOptions<SimulatorOptions> options)
{
    private volatile bool _running = options.Value.Enabled;

    public bool IsRunning => _running;
    public void Start() => _running = true;
    public void Stop() => _running = false;
}

/// <summary>
/// Background service that creates a fake transaction every 2–5 seconds and occasionally injects
/// a fraud pattern. Each transaction goes through <see cref="TransactionScorer"/> as it "happens".
/// </summary>
public sealed class TransactionSimulator(
    IServiceScopeFactory scopeFactory,
    SimulatorState state,
    IOptions<SimulatorOptions> options,
    ILogger<TransactionSimulator> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(250);

    private readonly Random _random = new();
    private readonly PriorityQueue<SimulatedTransaction, DateTime> _pending = new();
    private readonly Dictionary<int, DateTime> _accountAvailableAt = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var generator = await CreateGeneratorAsync(stoppingToken);
        if (generator is null)
            return;

        logger.LogInformation("Transaction simulator ready ({Accounts} accounts). Running: {Running}",
            generator.Accounts.Count, state.IsRunning);

        var nextBatchAt = DateTime.UtcNow;
        using var timer = new PeriodicTimer(Tick);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!state.IsRunning)
                continue;

            var now = DateTime.UtcNow;
            if (now >= nextBatchAt)
            {
                ScheduleNextBatch(generator, now);
                nextBatchAt = now + NextDelay();
            }

            while (_pending.TryPeek(out var item, out var dueAt) && dueAt <= now)
            {
                _pending.Dequeue();
                await ProcessAsync(item, stoppingToken);
            }
        }
    }

    private async Task<TransactionGenerator?> CreateGeneratorAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FraudDbContext>();

        var cities = UsCities.Home.Concat(UsCities.Distant).ToDictionary(c => c.Name);
        var accounts = await db.Accounts
            .AsNoTracking()
            .Select(a => new { a.Id, a.Customer!.HomeCity })
            .ToListAsync(ct);
        var merchants = await db.Merchants.AsNoTracking().ToListAsync(ct);

        var profiles = accounts
            .Where(a => cities.ContainsKey(a.HomeCity))
            .Select(a => new AccountProfile(a.Id, cities[a.HomeCity]))
            .ToList();

        if (profiles.Count == 0 || merchants.Count == 0)
        {
            logger.LogWarning("Transaction simulator disabled: no accounts or merchants in the database.");
            return null;
        }

        return new TransactionGenerator(profiles, merchants, _random);
    }

    private void ScheduleNextBatch(TransactionGenerator generator, DateTime now)
    {
        var free = generator.Accounts.Where(a => _accountAvailableAt.GetValueOrDefault(a.AccountId) <= now).ToList();
        if (free.Count == 0)
            return;

        var account = free[_random.Next(free.Count)];

        if (_random.NextDouble() < options.Value.FraudPatternChance)
        {
            var batch = generator.Pattern(generator.RandomPattern(), account);
            foreach (var item in batch.Items)
                _pending.Enqueue(item, now + item.Offset);

            _accountAvailableAt[account.AccountId] = now + batch.Items.Max(i => i.Offset) + batch.AccountCooldown;
            logger.LogInformation("Injecting fraud pattern {Pattern} on account {AccountId}", batch.Pattern, account.AccountId);
        }
        else
        {
            _pending.Enqueue(generator.Normal(account), now);
            _accountAvailableAt[account.AccountId] = now + TransactionGenerator.NormalCooldown;
        }
    }

    private async Task ProcessAsync(SimulatedTransaction item, CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var scorer = scope.ServiceProvider.GetRequiredService<TransactionScorer>();

            var transaction = item.ToTransaction(DateTime.UtcNow);
            var result = await scorer.ScoreAndSaveAsync(transaction, ct);

            if (result.IsFlagged)
            {
                logger.LogInformation("Alert opened for transaction {TransactionId} (score {Score}): {Rules}",
                    transaction.Id, result.Score, string.Join(", ", result.FiredRules.Select(f => f.Rule.Code)));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One bad transaction shouldn't stop the stream.
            logger.LogError(ex, "Failed to score simulated transaction for account {AccountId}", item.AccountId);
        }
    }

    private TimeSpan NextDelay()
    {
        var o = options.Value;
        var max = Math.Max(o.MinDelayMs, o.MaxDelayMs);
        return TimeSpan.FromMilliseconds(_random.Next(o.MinDelayMs, max + 1));
    }
}
