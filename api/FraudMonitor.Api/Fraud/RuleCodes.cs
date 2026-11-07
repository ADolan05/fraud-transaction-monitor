using FraudMonitor.Api.Models;

namespace FraudMonitor.Api.Fraud;

public static class RuleCodes
{
    public const string LargeAmount = "LARGE_AMOUNT";
    public const string Velocity = "VELOCITY";
    public const string ImpossibleTravel = "IMPOSSIBLE_TRAVEL";
    public const string OddHour = "ODD_HOUR";
    public const string RiskyCategory = "RISKY_CATEGORY";
    public const string CardTesting = "CARD_TESTING";
}

/// <summary>The starting rule configuration, inserted by the EF Core migration.</summary>
public static class DefaultRules
{
    public static readonly Rule[] All =
    [
        new()
        {
            Id = 1, Code = RuleCodes.LargeAmount, Name = "Large amount", Weight = 30, Threshold = 5m,
            Description = "Amount is more than [threshold] times the account's 30-day average (needs at least 5 past transactions).",
        },
        new()
        {
            Id = 2, Code = RuleCodes.Velocity, Name = "Velocity", Weight = 35, Threshold = 3m,
            Description = "[threshold] or more transactions on one account within 5 minutes.",
        },
        new()
        {
            Id = 3, Code = RuleCodes.ImpossibleTravel, Name = "Impossible travel", Weight = 45, Threshold = 500m,
            Description = "Two transactions at least 100 miles apart within 24 hours that would need travel faster than [threshold] mph.",
        },
        new()
        {
            Id = 4, Code = RuleCodes.OddHour, Name = "Odd hour", Weight = 15, Threshold = 0.10m,
            Description = "Transaction between 1 and 5 a.m. local time, for an account where less than [threshold] (a fraction) of the last 30 days' transactions happened then.",
        },
        new()
        {
            Id = 5, Code = RuleCodes.RiskyCategory, Name = "Risky category", Weight = 20, Threshold = 500m,
            Description = "Gift card, crypto exchange or wire service purchase above $[threshold].",
        },
        new()
        {
            Id = 6, Code = RuleCodes.CardTesting, Name = "Card testing", Weight = 40, Threshold = 3m,
            Description = "[threshold] or more charges under $2 in the past hour, followed by a charge of $100 or more.",
        },
    ];
}
