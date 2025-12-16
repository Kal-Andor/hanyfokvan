namespace HanyFokVan.Api.Models;

public class NearbyStation
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? DistanceKm { get; set; }
    public string Source { get; set; } = string.Empty;
}
