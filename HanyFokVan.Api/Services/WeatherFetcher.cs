using HanyFokVan.Api.Models;

namespace HanyFokVan.Api.Services;

public interface IWeatherFetcher
{
    Task<List<WeatherData>> FetchCurrentWeatherAsync();
}

public class WeatherFetcher : IWeatherFetcher
{
    private const string WundergroundUrl = "https://www.wunderground.com/dashboard/pws/IODORH15"; // Example
    private readonly HttpClient _httpClient;

    public WeatherFetcher(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<WeatherData>> FetchCurrentWeatherAsync()
    {
        // MVP: Returns dummy data for structure verification. 
        // Real implementation will use HtmlAgilityPack to parse Wunderground/Sensor.Community.
        
        var dummyData = new List<WeatherData>
        {
            new WeatherData
            {
                TemperatureC = 12.5,
                Source = "Wunderground (IODORH15)",
                Location = "Odorheiu Secuiesc",
                FetchedAt = DateTime.Now
            },
             new WeatherData
            {
                TemperatureC = 11.8,
                Source = "Sensor.Community",
                Location = "Odorheiu Secuiesc (Center)",
                FetchedAt = DateTime.Now
            }
        };

        return await Task.FromResult(dummyData);
    }
}
