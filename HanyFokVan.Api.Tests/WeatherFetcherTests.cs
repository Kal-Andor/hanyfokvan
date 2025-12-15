using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HanyFokVan.Api.Models;
using HanyFokVan.Api.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HanyFokVan.Api.Tests;

public class WeatherFetcherTests
{
    private static async Task<StationObservation?> InvokeFetchStationObservationAsync(WeatherFetcher sut, string stationId)
    {
        var mi = typeof(WeatherFetcher)
            .GetMethod("FetchStationObservationAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        mi.Should().NotBeNull("private method should exist");
        var task = (Task<StationObservation?>)mi!.Invoke(sut, new object[] { stationId })!;
        return await task.ConfigureAwait(false);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public List<HttpRequestMessage> Requests { get; } = new();

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responder(request));
        }
    }

    private static WeatherFetcher CreateSut(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHandler(responder);
        var httpClient = new HttpClient(handler);
        IConfiguration config = new ConfigurationBuilder().Build();
        return new WeatherFetcher(httpClient, config);
    }

    [Fact]
    public async Task FetchStationObservationAsync_returns_null_when_api_key_missing()
    {
        // Arrange
        Environment.SetEnvironmentVariable("WEATHER_API_KEY", null);
        var sut = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        });

        // Act
        var result = await InvokeFetchStationObservationAsync(sut, "TEST1");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FetchStationObservationAsync_returns_null_on_non_success_status()
    {
        // Arrange
        Environment.SetEnvironmentVariable("WEATHER_API_KEY", "dummy");
        var sut = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));

        // Act
        var result = await InvokeFetchStationObservationAsync(sut, "TEST2");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FetchStationObservationAsync_parses_temperature_from_valid_json()
    {
        // Arrange
        Environment.SetEnvironmentVariable("WEATHER_API_KEY", "dummy");
        var json = """
        {
          "observations": [
            {
            "humidity": 89.0,
              "metric": {
                "temp": 12.3
              }
            }
          ]
        }
        """;

        var sut = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        // Act
        var result = await InvokeFetchStationObservationAsync(sut, "TEST3");

        // Assert
        result.Should().NotBeNull();
        result!.TemperatureC.Should().Be(12.3);
    }

    [Fact]
    public async Task FetchStationObservationAsync_returns_null_when_observations_missing_or_empty()
    {
        // Arrange
        Environment.SetEnvironmentVariable("WEATHER_API_KEY", "dummy");
        var json = "{ }"; // no observations

        var sut = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        // Act
        var result = await InvokeFetchStationObservationAsync(sut, "TEST4");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FetchStationObservationAsync_returns_null_when_metric_missing() //TODO: should work without metric
    {
        // Arrange
        Environment.SetEnvironmentVariable("WEATHER_API_KEY", "dummy");
        var json = """
        {
          "observations": [
            {
              "humidity": 50
            }
          ]
        }
        """;

        var sut = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        // Act
        var result = await InvokeFetchStationObservationAsync(sut, "TEST5");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FetchStationObservationAsync_parses_all_metrics_from_complete_json()
    {
        // Arrange
        Environment.SetEnvironmentVariable("WEATHER_API_KEY", "dummy");
        var json = """
        {
          "observations": [{
            "humidity": 73.0,
            "metric": {
              "temp": 2.7,
              "pressure": 1030.82
            }
          }]
        }
        """;

        var sut = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        // Act
        var result = await InvokeFetchStationObservationAsync(sut, "TEST_ALL");

        // Assert
        result.Should().NotBeNull();
        result!.TemperatureC.Should().Be(2.7);
        result.Humidity.Should().Be(73.0);
        result.PressureMb.Should().Be(1030.82);
    }

    [Fact]
    public async Task FetchStationObservationAsync_handles_missing_humidity_gracefully()
    {
        // Arrange
        Environment.SetEnvironmentVariable("WEATHER_API_KEY", "dummy");
        var json = """
        {
          "observations": [{
            "metric": {
              "temp": 15.0,
              "pressure": 1015.0
            }
          }]
        }
        """;

        var sut = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        // Act
        var result = await InvokeFetchStationObservationAsync(sut, "TEST_NO_HUM");

        // Assert
        result.Should().NotBeNull();
        result!.TemperatureC.Should().Be(15.0);
        result.Humidity.Should().BeNull();
        result.PressureMb.Should().Be(1015.0);
    }

    [Fact]
    public async Task FetchStationObservationAsync_handles_missing_pressure_gracefully()
    {
        // Arrange
        Environment.SetEnvironmentVariable("WEATHER_API_KEY", "dummy");
        var json = """
        {
          "observations": [{
            "humidity": 80.0,
            "metric": {
              "temp": 10.5
            }
          }]
        }
        """;

        var sut = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        // Act
        var result = await InvokeFetchStationObservationAsync(sut, "TEST_NO_PRESS");

        // Assert
        result.Should().NotBeNull();
        result!.TemperatureC.Should().Be(10.5);
        result.Humidity.Should().Be(80.0);
        result.PressureMb.Should().BeNull();
    }
}
