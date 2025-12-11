using HanyFokVan.Api.Models;
using HanyFokVan.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HanyFokVan.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherController : ControllerBase
{
    private readonly IWeatherFetcher _weatherFetcher;
    private readonly ILogger<WeatherController> _logger;

    public WeatherController(IWeatherFetcher weatherFetcher, ILogger<WeatherController> logger)
    {
        _weatherFetcher = weatherFetcher;
        _logger = logger;
    }

    [HttpGet("current")]
    public async Task<ActionResult<List<WeatherData>>> GetCurrentWeather()
    {
        _logger.LogInformation("Fetching current weather data...");
        var data = await _weatherFetcher.FetchCurrentWeatherAsync();
        return Ok(data);
    }

    [HttpGet("nearby-stations")]
    public async Task<ActionResult<List<NearbyStation>>> GetNearbyStations([FromQuery] double lat, [FromQuery] double lon, CancellationToken ct)
    {
        if (double.IsNaN(lat) || double.IsNaN(lon))
        {
            return BadRequest("Invalid coordinates.");
        }

        _logger.LogInformation("Fetching nearby stations for lat={Lat}, lon={Lon}", lat, lon);
        try
        {
            var stations = await _weatherFetcher.GetNearbyStationsAsync(lat, lon, ct);
            return Ok(stations);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Nearby stations request failed due to configuration.");
            return StatusCode(500, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error when fetching nearby stations");
            return StatusCode(502, "Failed to fetch data from weather provider.");
        }
    }
}
