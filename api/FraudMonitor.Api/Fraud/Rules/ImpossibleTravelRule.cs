using FraudMonitor.Api.Models;

namespace FraudMonitor.Api.Fraud.Rules;

/// <summary>
/// Fires when a recent transaction was so far away that getting here in time would need
/// travel faster than [threshold] mph. Ignores hops under 100 miles, where GPS noise and
/// near-simultaneous timestamps would produce silly speeds.
/// </summary>
public sealed class ImpossibleTravelRule : IFraudRule
{
    public static readonly TimeSpan Lookback = TimeSpan.FromHours(24);
    public const double MinimumMiles = 100;

    public string Code => RuleCodes.ImpossibleTravel;

    public RuleResult Evaluate(Transaction transaction, IReadOnlyList<Transaction> history, decimal threshold)
    {
        var since = transaction.Timestamp - Lookback;
        var maxMph = (double)threshold;

        (double Miles, TimeSpan Elapsed, double Mph)? worst = null;

        foreach (var prior in history.Where(t => t.Timestamp >= since && t.Timestamp <= transaction.Timestamp))
        {
            var miles = Geo.Miles(prior.Latitude, prior.Longitude, transaction.Latitude, transaction.Longitude);
            if (miles < MinimumMiles)
                continue;

            var elapsed = transaction.Timestamp - prior.Timestamp;
            var hours = Math.Max(elapsed.TotalHours, 1.0 / 3600); // floor at one second
            var mph = miles / hours;

            if (mph > maxMph && (worst is null || mph > worst.Value.Mph))
                worst = (miles, elapsed, mph);
        }

        if (worst is not { } w)
            return RuleResult.Miss;

        return RuleResult.Fired(
            $"{Fmt.Number(w.Miles)} miles from a transaction {Fmt.Duration(w.Elapsed)} earlier (would need {Fmt.Number(w.Mph)} mph)");
    }
}
