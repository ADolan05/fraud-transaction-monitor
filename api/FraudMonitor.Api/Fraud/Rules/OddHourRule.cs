using System.Globalization;
using FraudMonitor.Api.Models;

namespace FraudMonitor.Api.Fraud.Rules;

/// <summary>
/// Fires for a transaction between 1:00 and 4:59 a.m. local time when less than [threshold]
/// (a fraction, e.g. 0.10) of the account's last 30 days of transactions happened in those hours.
/// </summary>
public sealed class OddHourRule : IFraudRule
{
    public static readonly TimeSpan Lookback = TimeSpan.FromDays(30);
    public const int StartHour = 1;
    public const int EndHour = 5; // exclusive

    public string Code => RuleCodes.OddHour;

    public static bool IsOddHour(DateTime local) => local.Hour >= StartHour && local.Hour < EndHour;

    public RuleResult Evaluate(Transaction transaction, IReadOnlyList<Transaction> history, decimal threshold)
    {
        var local = Geo.LocalTime(transaction.Timestamp, transaction.Longitude);
        if (!IsOddHour(local))
            return RuleResult.Miss;

        var since = transaction.Timestamp - Lookback;
        var past = history.Where(t => t.Timestamp >= since && t.Timestamp <= transaction.Timestamp).ToList();

        // An account with no history has no habit of night-time spending, so it counts as "rarely".
        var share = past.Count == 0
            ? 0m
            : (decimal)past.Count(t => IsOddHour(Geo.LocalTime(t.Timestamp, t.Longitude))) / past.Count;

        if (share >= threshold)
            return RuleResult.Miss;

        var time = local.ToString("h:mm tt", CultureInfo.InvariantCulture);
        return RuleResult.Fired(
            $"{time} local time; {Fmt.Number((double)share * 100)}% of this account's last 30 days was between 1 and 5 AM");
    }
}
