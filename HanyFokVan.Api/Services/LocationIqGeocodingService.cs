using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace HanyFokVan.Api.Services;

/// <summary>
/// Reverse geocoding service using LocationIQ API.
/// Converts coordinates to city names with caching support.
/// </summary>
public class LocationIqGeocodingService : IGeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LocationIqGeocodingService> _logger;
    private readonly string? _apiKey;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    public LocationIqGeocodingService(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<LocationIqGeocodingService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _apiKey = Environment.GetEnvironmentVariable("LOCATIONIQ_API_KEY");

        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("LOCATIONIQ_API_KEY not configured - reverse geocoding will be disabled");
        }
    }

    public async Task<string?> GetCityNameAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            return null;
        }

        // Round coordinates to 2 decimal places (~1km precision) for cache key
        var cacheKey = string.Format(CultureInfo.InvariantCulture, "geocode:{0:F2},{1:F2}", latitude, longitude);

        if (_cache.TryGetValue(cacheKey, out string? cachedCity))
        {
            _logger.LogDebug("Cache hit for geocoding: {CacheKey} -> {City}", cacheKey, cachedCity);
            return cachedCity;
        }

        try
        {
            var city = await FetchCityNameAsync(latitude, longitude, cancellationToken);

            // Cache the result (even null to avoid repeated failed requests)
            _cache.Set(cacheKey, city, CacheDuration);
            _logger.LogDebug("Cached geocoding result: {CacheKey} -> {City}", cacheKey, city);

            return city;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch city name for coordinates ({Lat}, {Lon})", latitude, longitude);
            return null;
        }
    }

    private async Task<string?> FetchCityNameAsync(double latitude, double longitude, CancellationToken cancellationToken)
    {
        var url = string.Format(
            CultureInfo.InvariantCulture,
            "https://us1.locationiq.com/v1/reverse?lat={0}&lon={1}&format=json&zoom=10&accept-language=hu&normalizeaddress=1&normalizecity=1&namedetails=1&key={2}",
            latitude,
            longitude,
            _apiKey);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(RequestTimeout);

        var response = await _httpClient.GetAsync(url, cts.Token);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cts.Token);
        using var doc = JsonDocument.Parse(json);

        // Extract address.city from response
        if (doc.RootElement.TryGetProperty("address", out var address) &&
            address.TryGetProperty("city", out var city))
        {
            return city.GetString();
        }

        _logger.LogDebug("No city found in LocationIQ response for ({Lat}, {Lon})", latitude, longitude);
        return null;
    }
}
