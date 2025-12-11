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
}
