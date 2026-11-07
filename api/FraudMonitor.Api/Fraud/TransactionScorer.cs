using FraudMonitor.Api.Data;
using FraudMonitor.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FraudMonitor.Api.Fraud;

/// <summary>
/// Scores a new transaction and saves it. Loads the account's recent history and the current rule
/// settings, runs the <see cref="RuleEngine"/>, and opens an Alert with its AlertReasons when flagged.
/// </summary>
public sealed class TransactionScorer(FraudDbContext db, RuleEngine engine, IOptions<FraudEngineOptions> options)
{
    /// <summary>The longest lookback any rule needs.</summary>
    public static readonly TimeSpan HistoryWindow = TimeSpan.FromDays(30);

    public async Task<ScoreResult> ScoreAndSaveAsync(Transaction transaction, CancellationToken ct = default)
    {
        // Always use the tracked merchant so EF doesn't try to insert a detached copy.
        transaction.Merchant = await db.Merchants.FindAsync([transaction.MerchantId], ct)
            ?? throw new InvalidOperationException($"Merchant {transaction.MerchantId} does not exist.");

        var since = transaction.Timestamp - HistoryWindow;
        var history = await db.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == transaction.AccountId
                        && t.Timestamp >= since
                        && t.Timestamp <= transaction.Timestamp)
            .ToListAsync(ct);

        // Read rules on every transaction so admin changes take effect immediately.
        var rules = await db.Rules.AsNoTracking().ToListAsync(ct);

        var result = engine.Evaluate(transaction, history, rules, options.Value.AlertThreshold);

        transaction.RiskScore = result.Score;
        transaction.Status = result.IsFlagged ? TransactionStatus.Flagged : TransactionStatus.Cleared;
        db.Transactions.Add(transaction);

        if (result.IsFlagged)
        {
            db.Alerts.Add(new Alert
            {
                Transaction = transaction,
                Score = result.Score,
                Status = AlertStatus.Open,
                CreatedAt = DateTime.UtcNow,
                Reasons = result.FiredRules
                    .Select(f => new AlertReason { RuleId = f.Rule.Id, Detail = f.Detail })
                    .ToList(),
            });
        }

        await db.SaveChangesAsync(ct);
        return result;
    }
}
