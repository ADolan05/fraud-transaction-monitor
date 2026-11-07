namespace FraudMonitor.Api.Models;

/// <summary>Merchant category. Some categories (gift cards, crypto, wire) drive the risky-category rule.</summary>
public enum MerchantCategory
{
    Grocery,
    Restaurant,
    GasStation,
    Retail,
    Electronics,
    Pharmacy,
    Travel,
    Entertainment,
    GiftCards,
    CryptoExchange,
    WireService,
}

public class Merchant
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public MerchantCategory Category { get; set; }
    public required string City { get; set; }
    public required string State { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
