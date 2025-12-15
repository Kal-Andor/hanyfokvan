namespace HanyFokVan.Api.Models;

/// <summary>
/// Represents weather observations from a single PWS station
/// </summary>
public class StationObservation
{
    public double? TemperatureC { get; set; }
    public double? Humidity { get; set; }        // Percentage (0-100)
    public double? PressureMb { get; set; }      // Millibars
    public string StationId { get; set; } = string.Empty;
}
