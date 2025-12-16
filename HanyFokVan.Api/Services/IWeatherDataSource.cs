using HanyFokVan.Api.Models;

namespace HanyFokVan.Api.Services;

/// <summary>
/// Strategy interface for weather data sources.
/// Each implementation fetches observations from a specific provider (Weather.com, Netatmo, etc.)
/// </summary>
public interface IWeatherDataSource
{
    /// <summary>
    /// Unique name identifying this data source (e.g., "Weather.com PWS", "Netatmo")
    /// </summary>
    string SourceName { get; }

    /// <summary>
    /// Indicates whether this data source is properly configured and can be used
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Fetches weather observations from stations near the specified coordinates
    /// </summary>
    /// <param name="latitude">Latitude of the location</param>
    /// <param name="longitude">Longitude of the location</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of station observations from this data source</returns>
    Task<List<StationObservation>> FetchObservationsAsync(double latitude, double longitude, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets nearby stations from this data source
    /// </summary>
    Task<List<NearbyStation>> GetNearbyStationsAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}
