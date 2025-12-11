namespace HanyFokVan.Api.Models;

public class WeatherData
{
    public double TemperatureC { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime FetchedAt { get; set; }
    // Add AirQuality properties later
}
