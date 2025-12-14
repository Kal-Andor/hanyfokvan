using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanyFokVan.Mobile.Models;
using System.Net.Http.Json;
using System.Diagnostics;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using System.Globalization;

namespace HanyFokVan.Mobile.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly HttpClient _httpClient;
    private System.Timers.Timer? _autoRefreshTimer;
    private bool _isAppForegrounded = true;
    
    [ObservableProperty]
    private ObservableCollection<WeatherData> _items = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotRefreshing))]
    private bool _isRefreshing;
    
    [ObservableProperty]
    private string _lastUpdatedText = "No data loaded";
    
    public bool IsNotRefreshing => !IsRefreshing;

    public MainViewModel()
    {
        _httpClient = new HttpClient();
        SetupLifecycleHandlers();
        StartAutoRefresh();
        // Auto-refresh on app load
        _ = Task.Run(async () => await LoadWeatherData());
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadWeatherData();
    }

    private async Task LoadWeatherData()
    {
        IsRefreshing = true;
        try
        {
            // Determine URL based on device
            string baseUrl = Constants.BaseUrl;
            string url = $"{baseUrl}/Weather/current";

            // Try to get device location to pass as query parameters
            var coords = await TryGetLocationAsync();
            if (coords.HasValue)
            {
                var (lat, lon) = coords.Value;
                url = $"{url}?lat={lat.ToString(CultureInfo.InvariantCulture)}&lon={lon.ToString(CultureInfo.InvariantCulture)}";
            }

            var data = await _httpClient.GetFromJsonAsync<List<WeatherData>>(url);
            
            if (data != null)
            {
                Items.Clear();
                foreach (var item in data)
                {
                    Items.Add(item);
                }
                LastUpdatedText = $"Updated: {DateTime.Now:HH:mm}";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error fetching data: {ex.Message}");
            // Only show alert for manual refresh, not auto-refresh
            if (IsRefreshing && !_autoRefreshTimer?.Enabled == true)
            {
                await Shell.Current.DisplayAlert("Connection Failed", 
                    $"Could not reach: {Constants.BaseUrl}\n\nError: {ex.Message}", "OK");
            }
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task<(double lat, double lon)?> TryGetLocationAsync()
    {
        try
        {
            // Check and request location permission
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    return null; // Permission denied; fallback to server default
                }
            }
            // TODO: refine this, investigate how long the cache lasts
#if !DEBUG
            // Try to get cached location first for speed
            var cached = await Geolocation.Default.GetLastKnownLocationAsync();
            if (cached != null)
            {
                return (cached.Latitude, cached.Longitude);
            }
#endif
            

            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
            var current = await Geolocation.Default.GetLocationAsync(request);
            if (current != null)
            {
                return (current.Latitude, current.Longitude);
            }
        }
        catch (FeatureNotSupportedException)
        {
            // Device doesn't support geolocation
        }
        catch (PermissionException)
        {
            // Permission denied
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Location fetch failed: {ex.Message}");
        }
        return null;
    }

    private void SetupLifecycleHandlers()
    {
        // Handle app foreground/background events
        // Platform lifecycle events can be wired in MauiProgram if needed.
    }

    internal void OnAppForegrounded()
    {
        _isAppForegrounded = true;
        StartAutoRefresh();
        // Refresh immediately when app comes to foreground
        _ = Task.Run(async () => await LoadWeatherData());
    }

    internal void OnAppBackgrounded()
    {
        _isAppForegrounded = false;
        StopAutoRefresh();
    }

    private void StartAutoRefresh()
    {
        StopAutoRefresh(); // Ensure no duplicate timers
        
        _autoRefreshTimer = new System.Timers.Timer(TimeSpan.FromMinutes(5).TotalMilliseconds);
        _autoRefreshTimer.Elapsed += async (sender, e) => await AutoRefreshTimer_Elapsed();
        _autoRefreshTimer.AutoReset = true;
        _autoRefreshTimer.Start();
        
        Debug.WriteLine("Auto-refresh timer started (5-minute intervals)");
    }

    private void StopAutoRefresh()
    {
        _autoRefreshTimer?.Stop();
        _autoRefreshTimer?.Dispose();
        _autoRefreshTimer = null;
        Debug.WriteLine("Auto-refresh timer stopped");
    }

    private async Task AutoRefreshTimer_Elapsed()
    {
        if (!_isAppForegrounded) return; // Only refresh when app is in foreground
        
        Debug.WriteLine("Auto-refresh triggered");
        await LoadWeatherData();
    }
}
