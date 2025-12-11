using HanyFokVan.Api.Models;
using Microsoft.Extensions.Configuration;

namespace HanyFokVan.Api.Services;

public interface IWeatherFetcher
{
    Task<List<WeatherData>> FetchCurrentWeatherAsync();
    Task<List<NearbyStation>> GetNearbyStationsAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}

public class WeatherFetcher : IWeatherFetcher
{
    private const string WundergroundUrl = "https://www.wunderground.com/dashboard/pws/IODORH15"; // Example
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    public WeatherFetcher(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey =
            configuration["Weather:ApiKey"]
            ?? Environment.GetEnvironmentVariable("WEATHER_API_KEY");
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

    public async Task<List<NearbyStation>> GetNearbyStationsAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("Weather API key is not configured. Set Weather:ApiKey or WEATHER_API_KEY.");
        }

        var url = $"https://api.weather.com/v3/location/near?geocode={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&product=pws&format=json&apiKey={_apiKey}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var stations = new List<NearbyStation>();

        if (doc.RootElement.TryGetProperty("location", out var locationEl))
        {
            // According to Weather.com v3 location/near (product=pws), fields are arrays of equal length
            int count = 0;
            if (locationEl.TryGetProperty("stationId", out var stationIdArr) && stationIdArr.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                count = stationIdArr.GetArrayLength();
            }

            for (int i = 0; i < count; i++)
            {
                string? id = SafeGetString(locationEl, "stationId", i);
                string? name = SafeGetString(locationEl, "stationName", i) ?? id;
                double? lat = SafeGetDouble(locationEl, "latitude", i);
                double? lon = SafeGetDouble(locationEl, "longitude", i);
                double? distanceKm = SafeGetDouble(locationEl, "distanceKm", i);

                if (!string.IsNullOrWhiteSpace(id))
                {
                    stations.Add(new NearbyStation
                    {
                        Id = id,
                        Name = name ?? id,
                        Latitude = lat,
                        Longitude = lon,
                        DistanceKm = distanceKm
                    });
                }
            }
        }

        return stations;

        static string? SafeGetString(System.Text.Json.JsonElement parent, string prop, int index)
        {
            if (parent.TryGetProperty(prop, out var arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array && index < arr.GetArrayLength())
            {
                var el = arr[index];
                return el.ValueKind == System.Text.Json.JsonValueKind.String ? el.GetString() : el.ToString();
            }
            return null;
        }

        static double? SafeGetDouble(System.Text.Json.JsonElement parent, string prop, int index)
        {
            if (parent.TryGetProperty(prop, out var arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array && index < arr.GetArrayLength())
            {
                var el = arr[index];
                if (el.ValueKind == System.Text.Json.JsonValueKind.Number && el.TryGetDouble(out var d)) return d;
                if (el.ValueKind == System.Text.Json.JsonValueKind.String && double.TryParse(el.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ds)) return ds;
            }
            return null;
        }
    }
}
