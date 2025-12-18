using HanyFokVan.Api.Services;

// Load environment variables from .env file (for local development)
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();

// Register weather data sources (Strategy pattern)
builder.Services.AddScoped<IWeatherDataSource, WeatherComDataSource>();
builder.Services.AddScoped<IWeatherDataSource, NetatmoDataSource>();

// Register geocoding service (optional - gracefully degrades if API key not configured)
builder.Services.AddScoped<IGeocodingService, LocationIqGeocodingService>();

// Register the aggregating weather fetcher that combines all data sources
builder.Services.AddScoped<IWeatherFetcher, AggregatingWeatherFetcher>();

builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

// Simple health check endpoint
app.MapGet("/healtz", () => "OK");

app.Run();
