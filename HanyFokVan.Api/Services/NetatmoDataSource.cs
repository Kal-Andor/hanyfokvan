using System.Net.Http.Headers;
using System.Text.Json;
using HanyFokVan.Api.Models;

namespace HanyFokVan.Api.Services;

/// <summary>
/// Weather data source implementation for Netatmo public weather stations API.
/// Uses OAuth2 authentication and fetches data from /getpublicdata endpoint.
/// </summary>
public class NetatmoDataSource : IWeatherDataSource
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NetatmoDataSource> _logger;

    private string? _accessToken;
    private string? _refreshToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    private readonly string? _clientId;
    private readonly string? _clientSecret;

    // Bounding box radius in degrees (approximately 5km at this latitude)
    private const double BoundingBoxRadius = 0.05;

    public string SourceName => "Netatmo";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_clientId) &&
        !string.IsNullOrWhiteSpace(_clientSecret) &&
        !string.IsNullOrWhiteSpace(_refreshToken);

    public NetatmoDataSource(HttpClient httpClient, IConfiguration configuration, ILogger<NetatmoDataSource> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        _clientId = Environment.GetEnvironmentVariable("NETATMO_CLIENT_ID")
            ?? _configuration["Netatmo:ClientId"];
        _clientSecret = Environment.GetEnvironmentVariable("NETATMO_CLIENT_SECRET")
            ?? _configuration["Netatmo:ClientSecret"];
        _refreshToken = Environment.GetEnvironmentVariable("NETATMO_REFRESH_TOKEN")
            ?? _configuration["Netatmo:RefreshToken"];
        _accessToken = Environment.GetEnvironmentVariable("NETATMO_ACCESS_TOKEN")
            ?? _configuration["Netatmo:AccessToken"];
    }

    public async Task<List<StationObservation>> FetchObservationsAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var observations = new List<StationObservation>();

        if (!IsConfigured)
        {
            _logger.LogDebug("[{Source}] Not configured, skipping", SourceName);
            return observations;
        }

        try
        {
            await EnsureValidTokenAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(_accessToken))
            {
                _logger.LogWarning("[{Source}] No valid access token available", SourceName);
                return observations;
            }

            // Calculate bounding box around the coordinates
            double latNe = latitude + BoundingBoxRadius;
            double lonNe = longitude + BoundingBoxRadius;
            double latSw = latitude - BoundingBoxRadius;
            double lonSw = longitude - BoundingBoxRadius;

            var culture = System.Globalization.CultureInfo.InvariantCulture;
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["lat_ne"] = latNe.ToString(culture),
                ["lon_ne"] = lonNe.ToString(culture),
                ["lat_sw"] = latSw.ToString(culture),
                ["lon_sw"] = lonSw.ToString(culture),
                ["filter"] = "true"
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.netatmo.com/api/getpublicdata")
            {
                Content = content
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("[{Source}] API returned {StatusCode}: {Error}", SourceName, response.StatusCode, errorContent);
                return observations;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            observations = ParsePublicDataResponse(json);

            _logger.LogDebug("[{Source}] Retrieved {Count} observations", SourceName, observations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Source}] Error fetching observations", SourceName);
        }

        return observations;
    }

    public async Task<List<NearbyStation>> GetNearbyStationsAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var stations = new List<NearbyStation>();

        if (!IsConfigured)
        {
            return stations;
        }

        try
        {
            await EnsureValidTokenAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(_accessToken))
            {
                return stations;
            }

            double latNe = latitude + BoundingBoxRadius;
            double lonNe = longitude + BoundingBoxRadius;
            double latSw = latitude - BoundingBoxRadius;
            double lonSw = longitude - BoundingBoxRadius;

            var culture = System.Globalization.CultureInfo.InvariantCulture;
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["lat_ne"] = latNe.ToString(culture),
                ["lon_ne"] = lonNe.ToString(culture),
                ["lat_sw"] = latSw.ToString(culture),
                ["lon_sw"] = lonSw.ToString(culture),
                ["filter"] = "true"
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.netatmo.com/api/getpublicdata")
            {
                Content = content
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return stations;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            stations = ParseStationsFromPublicData(json, latitude, longitude);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Source}] Error fetching nearby stations", SourceName);
        }

        return stations;
    }

    private List<StationObservation> ParsePublicDataResponse(string json)
    {
        var observations = new List<StationObservation>();

        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("body", out var body))
        {
            return observations;
        }

        foreach (var device in body.EnumerateArray())
        {
            var observation = ParseDeviceObservation(device);
            if (observation != null)
            {
                observations.Add(observation);
            }
        }

        return observations;
    }

    private StationObservation? ParseDeviceObservation(JsonElement device)
    {
        string? stationId = null;
        if (device.TryGetProperty("_id", out var idEl))
        {
            stationId = idEl.GetString();
        }

        if (string.IsNullOrWhiteSpace(stationId))
        {
            return null;
        }

        var observation = new StationObservation
        {
            StationId = stationId,
            Source = SourceName
        };

        // Try to get outdoor module data first (more relevant for weather)
        if (device.TryGetProperty("measures", out var measures))
        {
            foreach (var measureProp in measures.EnumerateObject())
            {
                var measureData = measureProp.Value;

                // Check for outdoor temperature data (type contains Temperature)
                if (measureData.TryGetProperty("type", out var typeArr) && typeArr.ValueKind == JsonValueKind.Array)
                {
                    var types = new List<string>();
                    foreach (var t in typeArr.EnumerateArray())
                    {
                        types.Add(t.GetString() ?? "");
                    }

                    if (measureData.TryGetProperty("res", out var res))
                    {
                        // Get the most recent measurement
                        foreach (var resProp in res.EnumerateObject())
                        {
                            if (resProp.Value.ValueKind == JsonValueKind.Array)
                            {
                                var values = resProp.Value;
                                int idx = 0;
                                foreach (var type in types)
                                {
                                    if (idx < values.GetArrayLength())
                                    {
                                        var val = values[idx];
                                        if (val.TryGetDouble(out var numVal))
                                        {
                                            switch (type.ToLowerInvariant())
                                            {
                                                case "temperature":
                                                    observation.TemperatureC = numVal;
                                                    break;
                                                case "humidity":
                                                    observation.Humidity = numVal;
                                                    break;
                                                case "pressure":
                                                    observation.PressureMb = numVal;
                                                    break;
                                            }
                                        }
                                    }
                                    idx++;
                                }
                            }
                            break; // Only get the first (most recent) measurement
                        }
                    }
                }
            }
        }

        // Only return if we got at least temperature
        if (!observation.TemperatureC.HasValue)
        {
            return null;
        }

        return observation;
    }

    private List<NearbyStation> ParseStationsFromPublicData(string json, double refLat, double refLon)
    {
        var stations = new List<NearbyStation>();

        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("body", out var body))
        {
            return stations;
        }

        foreach (var device in body.EnumerateArray())
        {
            string? id = null;
            double? lat = null;
            double? lon = null;

            if (device.TryGetProperty("_id", out var idEl))
            {
                id = idEl.GetString();
            }

            if (device.TryGetProperty("place", out var place))
            {
                if (place.TryGetProperty("location", out var location) && location.ValueKind == JsonValueKind.Array && location.GetArrayLength() >= 2)
                {
                    if (location[0].TryGetDouble(out var lonVal))
                        lon = lonVal;
                    if (location[1].TryGetDouble(out var latVal))
                        lat = latVal;
                }
            }

            if (!string.IsNullOrWhiteSpace(id))
            {
                double? distance = null;
                if (lat.HasValue && lon.HasValue)
                {
                    distance = CalculateDistanceKm(refLat, refLon, lat.Value, lon.Value);
                }

                stations.Add(new NearbyStation
                {
                    Id = id,
                    Name = $"Netatmo {id[^4..]}",
                    Latitude = lat,
                    Longitude = lon,
                    DistanceKm = distance,
                    Source = SourceName
                });
            }
        }

        return stations.OrderBy(s => s.DistanceKm ?? double.MaxValue).ToList();
    }

    private async Task EnsureValidTokenAsync(CancellationToken cancellationToken)
    {
        // If token is still valid (with 5 minute buffer), don't refresh
        if (!string.IsNullOrWhiteSpace(_accessToken) && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5))
        {
            return;
        }

        await RefreshAccessTokenAsync(cancellationToken);
    }

    private async Task RefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_refreshToken) || string.IsNullOrWhiteSpace(_clientId) || string.IsNullOrWhiteSpace(_clientSecret))
        {
            _logger.LogWarning("[{Source}] Cannot refresh token: missing credentials", SourceName);
            return;
        }

        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["refresh_token"] = _refreshToken
            });

            using var response = await _httpClient.PostAsync("https://api.netatmo.com/oauth2/token", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("[{Source}] Token refresh failed: {StatusCode} - {Error}", SourceName, response.StatusCode, error);
                return;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("access_token", out var accessTokenEl))
            {
                _accessToken = accessTokenEl.GetString();
            }

            if (doc.RootElement.TryGetProperty("refresh_token", out var refreshTokenEl))
            {
                var newRefreshToken = refreshTokenEl.GetString();
                if (!string.IsNullOrWhiteSpace(newRefreshToken))
                {
                    _refreshToken = newRefreshToken;
                    // Note: In production, you'd want to persist this new refresh token
                    _logger.LogDebug("[{Source}] Refresh token updated", SourceName);
                }
            }

            if (doc.RootElement.TryGetProperty("expires_in", out var expiresEl) && expiresEl.TryGetInt32(out var expiresIn))
            {
                _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);
            }
            else
            {
                // Default to 3 hours if not specified
                _tokenExpiry = DateTime.UtcNow.AddHours(3);
            }

            _logger.LogDebug("[{Source}] Token refreshed successfully, expires at {Expiry}", SourceName, _tokenExpiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Source}] Error refreshing access token", SourceName);
        }
    }

    private static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Earth's radius in km
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
