using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HanyFokVan.Api.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HanyFokVan.Api.Tests;

public class WeatherFetcherTests
{
    private static async Task<double?> InvokeFetchStationObservationAsync(WeatherFetcher sut, string stationId)
    {
        var mi = typeof(WeatherFetcher)
            .GetMethod("FetchStationObservationAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        mi.Should().NotBeNull("private method should exist");
        var task = (Task<double?>)mi!.Invoke(sut, new object[] { stationId })!;
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
        result.Should().Be(12.3);
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
    public async Task FetchStationObservationAsync_returns_null_when_metric_or_temp_missing()
    {
        // Arrange
        Environment.SetEnvironmentVariable("WEATHER_API_KEY", "dummy");
        var json = """
        {
          "observations": [
            {
              "metric": {}
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
}
