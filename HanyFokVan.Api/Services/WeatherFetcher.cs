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
            var observations = new List<StationObservation>();

            // Limit to avoid hitting rate limits if many stations exist
            foreach (var station in stations.Take(6))
            {
                try
                {
                    var obs = await FetchStationObservationAsync(station.Id);
                    if (obs != null)
                    {
                        observations.Add(obs);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue
                    Console.WriteLine($"Failed to fetch data for {station.Id}: {ex.Message}");
                }
            }

            if (observations.Count != 0)
            {
                var weatherData = AggregateObservations(observations, lat, lon);
                return [weatherData];
            }
        }
        catch (Exception ex)
        {
             // Fallback or rethrow
             Console.WriteLine($"Error calculating weather: {ex.Message}");
        }

        return new List<WeatherData>();
    }

    private WeatherData AggregateObservations(List<StationObservation> observations, double lat, double lon)
    {
        // Extract valid values for each metric
        var temperatures = observations.Select(o => o.TemperatureC)
                                      .Where(t => t.HasValue)
                                      .Select(t => t!.Value).ToList();
        var humidities = observations.Select(o => o.Humidity)
                                    .Where(h => h.HasValue)
                                    .Select(h => h!.Value).ToList();
        var pressures = observations.Select(o => o.PressureMb)
                                   .Where(p => p.HasValue)
                                   .Select(p => p!.Value).ToList();

        // Calculate labels
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        bool isDefault = Math.Abs(lat - 46.30) < 0.0001 && Math.Abs(lon - 25.30) < 0.0001;
        string locationLabel = isDefault
            ? "Odorheiu Secuiesc"
            : $"{lat.ToString("F4", culture)},{lon.ToString("F4", culture)}";
        string sourceLabel = isDefault
            ? $"Odorheiu Secuiesc (Mean of {temperatures.Count} stations)"
            : $"Nearby mean (Mean of {temperatures.Count} stations)";

        return new WeatherData
        {
            TemperatureC = Math.Round(temperatures.Average(), 1),
            Humidity = humidities.Any() ? Math.Round(humidities.Average(), 0) : null,
            PressureMb = pressures.Any() ? Math.Round(pressures.Average(), 1) : null,
            Source = sourceLabel,
            Location = locationLabel,
            FetchedAt = DateTime.Now,
        };
    }

    private async Task<StationObservation?> FetchStationObservationAsync(string stationId)
    {
         if (string.IsNullOrWhiteSpace(_apiKey)) return null;

         var url = $"https://api.weather.com/v2/pws/observations/current?stationId={stationId}&format=json&units=m&numericPrecision=decimal&apiKey={_apiKey}";

         using var response = await _httpClient.GetAsync(url);
         if (!response.IsSuccessStatusCode) return null;

         var json = await response.Content.ReadAsStringAsync();
         using var doc = System.Text.Json.JsonDocument.Parse(json);

         if (!doc.RootElement.TryGetProperty("observations", out var obs) || obs.GetArrayLength() <= 0) return null;

         var observation = obs[0];

         if (!observation.TryGetProperty("metric", out var metric))
             return null;

         var result = new StationObservation { StationId = stationId };

         // Temperature (in metric object)
         if (metric.TryGetProperty("temp", out var tempEl) && tempEl.TryGetDouble(out var temp))
             result.TemperatureC = temp;

         // Humidity (at the root level of observation)
         if (observation.TryGetProperty("humidity", out var humEl) && humEl.TryGetDouble(out var hum))
             result.Humidity = hum;

         // Pressure (in metric object)
         if (metric.TryGetProperty("pressure", out var pressEl) && pressEl.TryGetDouble(out var press))
             result.PressureMb = press;

         return result;
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
