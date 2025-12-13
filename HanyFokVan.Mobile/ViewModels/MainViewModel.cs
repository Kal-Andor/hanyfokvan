using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanyFokVan.Mobile.Models;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Timers;
using Microsoft.Maui.Devices;

namespace HanyFokVan.Mobile.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly HttpClient _httpClient;
    private System.Timers.Timer? _autoRefreshTimer;
    private bool _isAppForegrounded = true;
    
    [ObservableProperty]
    private ObservableCollection<WeatherData> items = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotRefreshing))]
    private bool isRefreshing;
    
    [ObservableProperty]
    private string lastUpdatedText = "No data loaded";
    
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

            var data = await _httpClient.GetFromJsonAsync<List<WeatherData>>($"{baseUrl}/Weather/current");
            
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
