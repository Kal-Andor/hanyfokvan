using HanyFokVan.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHttpClient();

// Register weather data sources (Strategy pattern)
builder.Services.AddScoped<IWeatherDataSource, WeatherComDataSource>();
builder.Services.AddScoped<IWeatherDataSource, NetatmoDataSource>();

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
