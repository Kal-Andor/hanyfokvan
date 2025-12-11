using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanyFokVan.Mobile.Models;
using System.Net.Http.Json;
using System.Diagnostics;

namespace HanyFokVan.Mobile.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly HttpClient _httpClient;
    
    [ObservableProperty]
    private ObservableCollection<WeatherData> items = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotRefreshing))]
    private bool isRefreshing;
    
    public bool IsNotRefreshing => !IsRefreshing;

    public MainViewModel()
    {
        _httpClient = new HttpClient();
    }

    [RelayCommand]
    private async Task Refresh()
    {
        IsRefreshing = true;
        try
        {
            // Determine URL based on device
            string baseUrl = Constants.BaseUrl;

             // Note: using HTTP to avoid SSL certificate issues in emulator for MVP
             // If the API enforces HTTPS, we need the HTTPS port (usually 7xxx) and trust certificates.
             // I'll assume HTTP is available or I will fix the port.

            var data = await _httpClient.GetFromJsonAsync<List<WeatherData>>($"{baseUrl}/Weather/current");
            
            if (data != null)
            {
                Items.Clear();
                foreach (var item in data)
                {
                    Items.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error fetching data: {ex.Message}");
            // In a real app, show alert
        }
        finally
        {
            IsRefreshing = false;
        }
    }
}
