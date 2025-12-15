using HanyFokVan.Api.Models;
using Microsoft.Extensions.Configuration;

namespace HanyFokVan.Api.Services;

public interface IWeatherFetcher
{
    Task<List<WeatherData>> FetchCurrentWeatherAsync(double? latitude = null, double? longitude = null, CancellationToken cancellationToken = default);
    Task<List<NearbyStation>> GetNearbyStationsAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}

public class WeatherFetcher : IWeatherFetcher
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    public WeatherFetcher(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = Environment.GetEnvironmentVariable("WEATHER_API_KEY");
    }

    public async Task<List<WeatherData>> FetchCurrentWeatherAsync(double? latitude = null, double? longitude = null, CancellationToken cancellationToken = default)
    {
        // Default to Odorheiu Secuiesc coordinates if not provided
        double lat = latitude ?? 46.30;
        double lon = longitude ?? 25.30;

        try 
        {
            var stations = await GetNearbyStationsAsync(lat, lon, cancellationToken);
            var temperatures = new List<double>();
            
            // Limit to avoid hitting rate limits if many stations exist
            foreach (var station in stations.Take(6))
            {
                try 
                {
                    double? temp = await FetchStationObservationAsync(station.Id);
                    if (temp.HasValue)
                    {
                        temperatures.Add(temp.Value);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue
                    Console.WriteLine($"Failed to fetch data for {station.Id}: {ex.Message}");
                }
            }

            if (temperatures.Count != 0)
            {
                double meanTemp = temperatures.Average();
                var culture = System.Globalization.CultureInfo.InvariantCulture;
                bool isDefault = Math.Abs(lat - 46.30) < 0.0001 && Math.Abs(lon - 25.30) < 0.0001;
                string locationLabel = isDefault ? "Odorheiu Secuiesc" : $"{lat.ToString("F4", culture)},{lon.ToString("F4", culture)}";
                string sourceLabel = isDefault
                    ? $"Odorheiu Secuiesc (Mean of {temperatures.Count} stations)"
                    : $"Nearby mean (Mean of {temperatures.Count} stations)";
                return
                [
                    new WeatherData
                    {
                        TemperatureC = Math.Round(meanTemp, 1),
                        Source = sourceLabel,
                        Location = locationLabel,
                        FetchedAt = DateTime.Now,
                    }
                ];
            }
        }
        catch (Exception ex)
        {
             // Fallback or rethrow
             Console.WriteLine($"Error calculating weather: {ex.Message}");
        }

        return new List<WeatherData>();
    }

    private async Task<double?> FetchStationObservationAsync(string stationId)
    {
         if (string.IsNullOrWhiteSpace(_apiKey)) return null;

         var url = $"https://api.weather.com/v2/pws/observations/current?stationId={stationId}&format=json&units=m&numericPrecision=decimal&apiKey={_apiKey}";
         
         using var response = await _httpClient.GetAsync(url);
         if (!response.IsSuccessStatusCode) return null;

         var json = await response.Content.ReadAsStringAsync();
         using var doc = System.Text.Json.JsonDocument.Parse(json);

         if (!doc.RootElement.TryGetProperty("observations", out var obs) || obs.GetArrayLength() <= 0) return null;
         
         var first = obs[0];
         
         if (!first.TryGetProperty("metric", out var metric) ||
             !metric.TryGetProperty("temp", out var tempEl)) 
             return null;
         
         if (tempEl.TryGetDouble(out var temp)) return temp;
         
         return null;
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
