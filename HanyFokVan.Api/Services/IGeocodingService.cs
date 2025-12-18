namespace HanyFokVan.Api.Services;

/// <summary>
/// Service for converting coordinates to city/location names via reverse geocoding.
/// </summary>
public interface IGeocodingService
{
    /// <summary>
    /// Gets the city name for the given coordinates.
    /// </summary>
    /// <param name="latitude">Latitude coordinate</param>
    /// <param name="longitude">Longitude coordinate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>City name if found, null otherwise</returns>
    Task<string?> GetCityNameAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}
