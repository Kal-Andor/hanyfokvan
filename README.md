# HanyFokVan

A cross-platform Weather & Air Quality monitoring application for **Odorheiu Secuiesc**.

## Architecture
- **Backend:** ASP.NET Core Web API 9.0 (`HanyFokVan.Api`)
- **Frontend:** .NET MAUI 9.0 (`HanyFokVan.Mobile`)

## Getting Started

### Prerequisites
- .NET 9.0 SDK
- MAUI Workload (`dotnet workload install maui`)
- Android Emulator or Device

### 1. Running the Backend
The backend serves the weather data.

1. Open a terminal in the root directory.
2. Run the API:
   ```powershell
   dotnet run --project HanyFokVan.Api/HanyFokVan.Api.csproj --launch-profile http
   ```
   > **Note**: usage of `--launch-profile http` ensures it runs on port `5286` (HTTP) which matches the mobile app configuration.

3. Verify it's running by visiting: http://localhost:5286/weather/current

### 2. Running the Mobile App (MAUI)

#### Option A: Windows
```powershell
dotnet build -t:Run -f net9.0-windows10.0.19041.0 HanyFokVan.Mobile/HanyFokVan.Mobile.csproj
```

#### Option B: Android
Start your emulator first, then:
```powershell
dotnet build -t:Run -f net9.0-android HanyFokVan.Mobile/HanyFokVan.Mobile.csproj
```
