using System.Globalization;

namespace FraudMonitor.Api.Fraud;

public static class Geo
{
    private const double EarthRadiusMiles = 3958.8;

    /// <summary>Great-circle (haversine) distance in miles.</summary>
    public static double Miles(double lat1, double lon1, double lat2, double lon2)
    {
        static double Rad(double deg) => deg * Math.PI / 180;

        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return EarthRadiusMiles * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    // Approximate US time zone from longitude. Good enough for synthetic data in the continental US.
    private static readonly (double MinLongitude, string IanaId, int StandardOffsetHours)[] Zones =
    [
        (-87.5, "America/New_York", -5),
        (-101.0, "America/Chicago", -6),
        (-115.0, "America/Denver", -7),
        (double.NegativeInfinity, "America/Los_Angeles", -8),
    ];

    /// <summary>Converts a UTC timestamp to local time at the given longitude (DST-aware where the OS has tz data).</summary>
    public static DateTime LocalTime(DateTime utc, double longitude)
    {
        var zone = Zones.First(z => longitude > z.MinLongitude);
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(zone.IanaId);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateTime.SpecifyKind(utc.AddHours(zone.StandardOffsetHours), DateTimeKind.Unspecified);
        }
    }
}

/// <summary>Formatting for rule explanations. Invariant culture so text doesn't depend on the server's locale.</summary>
public static class Fmt
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Money(decimal amount) => "$" + amount.ToString("N2", Inv);

    public static string Number(double value, string format = "N0") => value.ToString(format, Inv);

    public static string Duration(TimeSpan span)
    {
        if (span.TotalSeconds < 90)
        {
            var s = (int)Math.Round(span.TotalSeconds);
            return s == 1 ? "1 second" : $"{s} seconds";
        }
        if (span.TotalMinutes < 90)
        {
            var m = (int)Math.Round(span.TotalMinutes);
            return $"{m} minutes";
        }
        return $"{span.TotalHours.ToString("0.#", Inv)} hours";
    }
}
