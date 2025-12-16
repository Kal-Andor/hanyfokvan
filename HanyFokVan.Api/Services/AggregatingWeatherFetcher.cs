using HanyFokVan.Api.Models;

namespace HanyFokVan.Api.Services;

/// <summary>
/// Aggregates weather data from multiple data sources using the Strategy pattern.
/// Combines observations from all configured sources and returns averaged weather data.
/// </summary>
public class AggregatingWeatherFetcher : IWeatherFetcher
{
    private readonly IEnumerable<IWeatherDataSource> _dataSources;
    private readonly ILogger<AggregatingWeatherFetcher> _logger;

    public AggregatingWeatherFetcher(IEnumerable<IWeatherDataSource> dataSources, ILogger<AggregatingWeatherFetcher> logger)
    {
        _dataSources = dataSources;
        _logger = logger;
    }

    public async Task<List<WeatherData>> FetchCurrentWeatherAsync(double? latitude = null, double? longitude = null, CancellationToken cancellationToken = default)
    {
        // Default to Odorheiu Secuiesc coordinates if not provided
        double lat = latitude ?? 46.30;
        double lon = longitude ?? 25.30;

        var allObservations = new List<StationObservation>();
        var sourceCounts = new Dictionary<string, int>();

        // Fetch from all configured data sources in parallel
        var configuredSources = _dataSources.Where(s => s.IsConfigured).ToList();

        if (configuredSources.Count == 0)
        {
            _logger.LogWarning("No weather data sources are configured");
            return new List<WeatherData>();
        }

        _logger.LogDebug("Fetching weather data from {Count} sources: {Sources}",
            configuredSources.Count,
            string.Join(", ", configuredSources.Select(s => s.SourceName)));

        var fetchTasks = configuredSources.Select(async source =>
        {
            try
            {
                var observations = await source.FetchObservationsAsync(lat, lon, cancellationToken);
                return (source.SourceName, Observations: observations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching from {Source}", source.SourceName);
                return (source.SourceName, Observations: new List<StationObservation>());
            }
        });

        var results = await Task.WhenAll(fetchTasks);

        foreach (var (sourceName, observations) in results)
        {
            if (observations.Count > 0)
            {
                allObservations.AddRange(observations);
                sourceCounts[sourceName] = observations.Count;
                _logger.LogDebug("Got {Count} observations from {Source}", observations.Count, sourceName);
            }
        }

        if (allObservations.Count != 0)
        {
            var weatherData = AggregateObservations(allObservations, lat, lon, sourceCounts);
            return [weatherData];
        }

        _logger.LogWarning("No observations received from any data source");
        return new List<WeatherData>();
    }

    public async Task<List<NearbyStation>> GetNearbyStationsAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var allStations = new List<NearbyStation>();

        var configuredSources = _dataSources.Where(s => s.IsConfigured).ToList();

        var fetchTasks = configuredSources.Select(async source =>
        {
            try
            {
                return await source.GetNearbyStationsAsync(latitude, longitude, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting nearby stations from {Source}", source.SourceName);
                return new List<NearbyStation>();
            }
        });

        var results = await Task.WhenAll(fetchTasks);

        foreach (var stations in results)
        {
            allStations.AddRange(stations);
        }

        // Sort by distance
        return allStations.OrderBy(s => s.DistanceKm ?? double.MaxValue).ToList();
    }

    private WeatherData AggregateObservations(List<StationObservation> observations, double lat, double lon, Dictionary<string, int> sourceCounts)
    {
        var temperatures = observations.Select(o => o.TemperatureC)
                                      .Where(t => t.HasValue)
                                      .Select(t => t!.Value).ToList();
        var humidities = observations.Select(o => o.Humidity)
                                    .Where(h => h.HasValue)
                                    .Select(h => h!.Value).ToList();
        var pressures = observations.Select(o => o.PressureMb)
                                   .Where(p => p.HasValue)
                                   .Select(p => p!.Value).ToList();

        var culture = System.Globalization.CultureInfo.InvariantCulture;
        bool isDefault = Math.Abs(lat - 46.30) < 0.0001 && Math.Abs(lon - 25.30) < 0.0001;

        string locationLabel = isDefault
            ? "Odorheiu Secuiesc"
            : $"{lat.ToString("F4", culture)},{lon.ToString("F4", culture)}";

        // Build source description showing count from each source
        var sourceDetails = string.Join(", ", sourceCounts.Select(kv => $"{kv.Value} {kv.Key}"));
        string sourceLabel = isDefault
            ? $"Odorheiu Secuiesc (Mean of {temperatures.Count} stations: {sourceDetails})"
            : $"Nearby mean (Mean of {temperatures.Count} stations: {sourceDetails})";

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
}
