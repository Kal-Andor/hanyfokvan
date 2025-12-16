using System.Text.Json;
using HanyFokVan.Api.Models;

namespace HanyFokVan.Api.Services;

/// <summary>
/// Weather data source implementation for Weather.com Personal Weather Stations (PWS) API
/// </summary>
public class WeatherComDataSource : IWeatherDataSource
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    public string SourceName => "Weather.com PWS";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public WeatherComDataSource(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _apiKey = Environment.GetEnvironmentVariable("WEATHER_API_KEY");
    }

    public async Task<List<StationObservation>> FetchObservationsAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var observations = new List<StationObservation>();

        if (!IsConfigured) return observations;

        try
        {
            var stations = await GetNearbyStationsAsync(latitude, longitude, cancellationToken);

            foreach (var station in stations.Take(6))
            {
                try
                {
                    var obs = await FetchStationObservationAsync(station.Id, cancellationToken);
                    if (obs != null)
                    {
                        observations.Add(obs);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{SourceName}] Failed to fetch data for {station.Id}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{SourceName}] Error fetching observations: {ex.Message}");
        }

        return observations;
    }

    public async Task<List<NearbyStation>> GetNearbyStationsAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Weather API key is not configured. Set WEATHER_API_KEY environment variable.");
        }

        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var url = $"https://api.weather.com/v3/location/near?geocode={latitude.ToString(culture)},{longitude.ToString(culture)}&product=pws&format=json&apiKey={_apiKey}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var stations = new List<NearbyStation>();

        if (doc.RootElement.TryGetProperty("location", out var locationEl))
        {
            int count = 0;
            if (locationEl.TryGetProperty("stationId", out var stationIdArr) && stationIdArr.ValueKind == JsonValueKind.Array)
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
                        DistanceKm = distanceKm,
                        Source = SourceName
                    });
                }
            }
        }

        return stations;
    }

    private async Task<StationObservation?> FetchStationObservationAsync(string stationId, CancellationToken cancellationToken = default)
    {
        var url = $"https://api.weather.com/v2/pws/observations/current?stationId={stationId}&format=json&units=m&numericPrecision=decimal&apiKey={_apiKey}";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("observations", out var obs) || obs.GetArrayLength() <= 0) return null;

        var observation = obs[0];

        if (!observation.TryGetProperty("metric", out var metric))
            return null;

        var result = new StationObservation
        {
            StationId = stationId,
            Source = SourceName
        };

        if (metric.TryGetProperty("temp", out var tempEl) && tempEl.TryGetDouble(out var temp))
            result.TemperatureC = temp;

        if (observation.TryGetProperty("humidity", out var humEl) && humEl.TryGetDouble(out var hum))
            result.Humidity = hum;

        if (metric.TryGetProperty("pressure", out var pressEl) && pressEl.TryGetDouble(out var press))
            result.PressureMb = press;

        return result;
    }

    private static string? SafeGetString(JsonElement parent, string prop, int index)
    {
        if (parent.TryGetProperty(prop, out var arr) && arr.ValueKind == JsonValueKind.Array && index < arr.GetArrayLength())
        {
            var el = arr[index];
            return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
        }
        return null;
    }

    private static double? SafeGetDouble(JsonElement parent, string prop, int index)
    {
        if (parent.TryGetProperty(prop, out var arr) && arr.ValueKind == JsonValueKind.Array && index < arr.GetArrayLength())
        {
            var el = arr[index];
            if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var d)) return d;
            if (el.ValueKind == JsonValueKind.String && double.TryParse(el.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ds)) return ds;
        }
        return null;
    }
}
