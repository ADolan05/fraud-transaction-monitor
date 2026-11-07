namespace FraudMonitor.Api.Data;

public record City(string Name, string State, double Latitude, double Longitude);

/// <summary>
/// Cities used to place customers and merchants. Customers live in the northeast
/// (the "home" footprint); a few far-away cities exist so the simulator can create
/// impossible-travel patterns like Buffalo → Miami in 20 minutes.
/// </summary>
public static class UsCities
{
    public static readonly City[] Home =
    [
        new("Buffalo", "NY", 42.8864, -78.8784),
        new("Rochester", "NY", 43.1566, -77.6088),
        new("Syracuse", "NY", 43.0481, -76.1474),
        new("Albany", "NY", 42.6526, -73.7562),
        new("New York", "NY", 40.7128, -74.0060),
        new("Baltimore", "MD", 39.2904, -76.6122),
        new("Philadelphia", "PA", 39.9526, -75.1652),
        new("Pittsburgh", "PA", 40.4406, -79.9959),
        new("Wilmington", "DE", 39.7391, -75.5398),
        new("Washington", "DC", 38.9072, -77.0369),
        new("Hartford", "CT", 41.7658, -72.6734),
    ];

    public static readonly City[] Distant =
    [
        new("Miami", "FL", 25.7617, -80.1918),
        new("Chicago", "IL", 41.8781, -87.6298),
        new("Los Angeles", "CA", 34.0522, -118.2437),
        new("Las Vegas", "NV", 36.1699, -115.1398),
        new("Dallas", "TX", 32.7767, -96.7970),
        new("Seattle", "WA", 47.6062, -122.3321),
        new("Atlanta", "GA", 33.7490, -84.3880),
        new("Denver", "CO", 39.7392, -104.9903),
    ];
}
