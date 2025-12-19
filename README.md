# HanyFokVan - Weather App for Székelyudvarhely

A cross-platform weather monitoring application for **Odorheiu Secuiesc** (Székelyudvarhely), Romania - my hometown.

[![Live API](https://img.shields.io/badge/API-Live-brightgreen)](https://hanyfokvan-api.onrender.com/weather/current)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![MAUI](https://img.shields.io/badge/MAUI-9.0-512BD4)](https://dotnet.microsoft.com/apps/maui)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)

---

## Try It Now

### Download the Android App

**[Download APK (v1.0.0)](https://github.com/Kal-Andor/hanyfokvan/releases/download/v1.0.0/HanyFokVan-v1.0.0.apk)** (~27 MB)

> Note: You'll need to enable "Install from unknown sources" on your Android device.

### Try the Live API

The API is deployed and publicly accessible:

```bash
# Get current weather for Székelyudvarhely
curl https://hanyfokvan-api.onrender.com/weather/current
```

---

## Screenshots

### Mobile App
<!-- TODO: Add screenshot of main app screen -->
![App Screenshot](docs/screenshots/app-main.png)

### Android Widgets
<!-- TODO: Add screenshots of widgets -->
| Simple Widget | Detailed Widget |
|:-------------:|:---------------:|
| ![Simple](docs/screenshots/widget-simple.png) | ![Detailed](docs/screenshots/widget-detailed.png) |

---

## API Reference

**Base URL:** `https://hanyfokvan-api.onrender.com`

### Get Current Weather

```http
GET /weather/current
```

Returns aggregated weather data from multiple local weather stations.

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `lat` | double | No | Latitude (must be provided with `lon`) |
| `lon` | double | No | Longitude (must be provided with `lat`) |

**Example Request:**
```bash
# Default location (Székelyudvarhely)
curl https://hanyfokvan-api.onrender.com/weather/current

# Custom location
curl "https://hanyfokvan-api.onrender.com/weather/current?lat=46.30&lon=25.30"
```

**Example Response:**
```json
{
  "temperatureC": 12.5,
  "humidity": 78,
  "pressureMb": 1013.25,
  "source": "Aggregated",
  "location": "Székelyudvarhely",
  "fetchedAt": "2024-12-18T15:30:00Z"
}
```

### Get Nearby Stations

```http
GET /weather/nearby-stations?lat={lat}&lon={lon}
```

Returns a list of weather stations near the specified coordinates.

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `lat` | double | Yes | Latitude |
| `lon` | double | Yes | Longitude |

**Example Request:**
```bash
curl "https://hanyfokvan-api.onrender.com/weather/nearby-stations?lat=46.30&lon=25.30"
```

**Example Response:**
```json
[
  {
    "id": "STATION123",
    "name": "Székelyudvarhely Central",
    "latitude": 46.3050,
    "longitude": 25.2970,
    "distanceKm": 0.5,
    "source": "WeatherCom"
  }
]
```

### Health Check

```http
GET /healthz
```

Returns API health status.

---

## About This Project

This is a hobby/portfolio project I built to:

1. **Solve a real problem** - Get accurate local weather by aggregating data from multiple personal weather stations
2. **Learn new technologies** - Explore .NET MAUI for cross-platform development
3. **Practice software architecture** - Implement patterns like Strategy Pattern for data source abstraction

### What I Built

- **Backend API** - ASP.NET Core 9.0 Web API with Docker deployment
- **Mobile App** - .NET MAUI 9.0 cross-platform app (Android focus)
- **Android Widgets** - Home screen widgets for quick weather access
- **Data Aggregation** - Combines data from Weather.com PWS and Netatmo stations

### What I Learned

| Area | Technologies & Concepts |
|------|------------------------|
| **Backend** | ASP.NET Core 9.0, REST API design, Dependency Injection |
| **Mobile** | .NET MAUI 9.0, MVVM with CommunityToolkit, Android Widgets |
| **Architecture** | Strategy Pattern, Interface segregation, Data aggregation |
| **Integration** | OAuth2 (Netatmo), REST API consumption, Reverse geocoding (LocationIQ) |
| **DevOps** | Docker containerization, Render.com deployment, GitHub Actions |

---

## Architecture

```
┌─────────────────┐     ┌──────────────────────────────────────────┐
│  Mobile App     │────▶│           HanyFokVan.Api                 │
│  (.NET MAUI)    │     │                                          │
├─────────────────┤     │  ┌─────────────────────────────────────┐ │
│  - Main View    │     │  │     AggregatingWeatherFetcher       │ │
│  - Widgets (2)  │     │  │                                     │ │
│  - Auto-refresh │     │  │  ┌───────────┐   ┌───────────────┐  │ │
└─────────────────┘     │  │  │WeatherCom │   │   Netatmo     │  │ │
                        │  │  │DataSource │   │  DataSource   │  │ │
                        │  │  └─────┬─────┘   └───────┬───────┘  │ │
                        │  │        │                 │          │ │
                        │  └────────┼─────────────────┼──────────┘ │
                        │           ▼                 ▼            │
                        │     Weather.com        Netatmo API       │
                        │     PWS API            (OAuth2)          │
                        │                                          │
                        │  ┌─────────────────────────────────────┐ │
                        │  │       IGeocodingService             │ │
                        │  │  (LocationIQ reverse geocoding)     │ │
                        │  └─────────────────────────────────────┘ │
                        └──────────────────────────────────────────┘
```

**Key Design Decisions:**
- **Strategy Pattern** - Each data source (Weather.com, Netatmo) implements `IWeatherDataSource`, making it easy to add new sources
- **Graceful Degradation** - Missing API keys don't crash the app; unconfigured sources are simply skipped
- **Data Aggregation** - Averages readings from all available stations for more accurate local weather
- **Reverse Geocoding** - `IGeocodingService` converts coordinates to human-readable city names (via LocationIQ API with 24-hour caching)

---

## Tech Stack

| Component | Technology |
|-----------|------------|
| Backend | ASP.NET Core 9.0, C# 13 |
| Mobile | .NET MAUI 9.0 |
| MVVM | CommunityToolkit.Mvvm |
| Testing | xUnit, FluentAssertions |
| Container | Docker |
| Hosting | Render.com (Free tier) |
| CI/CD | GitHub Actions |

---

## Local Development

For detailed setup instructions, see [CLAUDE.md](CLAUDE.md).

**Quick Start:**
```bash
# Run the API
dotnet run --project HanyFokVan.Api/HanyFokVan.Api.csproj --launch-profile http

# Run tests
dotnet test

# Build Android APK
dotnet publish HanyFokVan.Mobile/HanyFokVan.Mobile.csproj -f net9.0-android -c Release -p:AndroidPackageFormat=apk
```

---

## License

This project is open source and available under the [GPL-3.0 License](LICENSE).

---

*Built with curiosity and coffee in Székelyudvarhely*
