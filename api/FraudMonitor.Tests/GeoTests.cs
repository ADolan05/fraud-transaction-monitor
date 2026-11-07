using FraudMonitor.Api.Fraud;
using static FraudMonitor.Tests.TestData;

namespace FraudMonitor.Tests;

public class GeoTests
{
    [Fact]
    public void Miles_BuffaloToMiami_IsAbout1180()
    {
        var miles = Geo.Miles(Buffalo.Latitude, Buffalo.Longitude, Miami.Latitude, Miami.Longitude);

        Assert.InRange(miles, 1_170, 1_190);
    }

    [Fact]
    public void Miles_SamePoint_IsZero()
    {
        Assert.Equal(0, Geo.Miles(Buffalo.Latitude, Buffalo.Longitude, Buffalo.Latitude, Buffalo.Longitude), 6);
    }

    [Theory]
    [InlineData(-78.88, 2026, 1, 15, 12, 7)]   // Buffalo, winter: UTC-5
    [InlineData(-78.88, 2026, 7, 15, 12, 8)]   // Buffalo, summer: UTC-4
    [InlineData(-87.63, 2026, 1, 15, 12, 6)]   // Chicago: UTC-6
    [InlineData(-118.24, 2026, 1, 15, 12, 4)]  // Los Angeles: UTC-8
    public void LocalTime_UsesTimeZoneForLongitude(double lon, int y, int m, int d, int utcHour, int expectedLocalHour)
    {
        var utc = new DateTime(y, m, d, utcHour, 0, 0, DateTimeKind.Utc);

        Assert.Equal(expectedLocalHour, Geo.LocalTime(utc, lon).Hour);
    }
}
