namespace HanyFokVan.Mobile.Models;

public class WeatherData
{
    public double TemperatureC { get; set; }
    public double? Humidity { get; set; }      // Percentage (0-100)
    public double? PressureMb { get; set; }    // Millibars
    public string Source { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime FetchedAt { get; set; }
}
