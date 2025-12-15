#if ANDROID
using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using Android.Util;
using Android.Widget;
using HanyFokVan.Mobile;
using HanyFokVan.Mobile.Models;
using System.Net.Http.Json;
using System.Globalization;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ALocation = Android.Locations.Location;

namespace HanyFokVan.Mobile.Platforms.Android.Widget;

[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate, ACTION_REFRESH })]
[MetaData(AppWidgetManager.MetaDataAppwidgetProvider, Resource = "@xml/weather_widget_detailed_info")]
public class WeatherWidgetDetailedProvider : AppWidgetProvider
{
    public const string ACTION_REFRESH = "com.companyname.hanyfokvan.WIDGET_DETAILED_REFRESH";
    private const string LogTag = "WeatherWidgetDetailed";

    public override void OnUpdate(Context context, AppWidgetManager appWidgetManager, int[] appWidgetIds)
    {
        base.OnUpdate(context, appWidgetManager, appWidgetIds);
        _ = UpdateAllAsync(context, appWidgetManager, appWidgetIds);
    }

    public override void OnReceive(Context context, Intent intent)
    {
        base.OnReceive(context, intent);
        if (intent?.Action == ACTION_REFRESH || intent?.Action == AppWidgetManager.ActionAppwidgetUpdate)
        {
            var appWidgetManager = AppWidgetManager.GetInstance(context);
            var thisWidget = new ComponentName(context, Java.Lang.Class.FromType(typeof(WeatherWidgetDetailedProvider)).Name);
            var appWidgetIds = appWidgetManager.GetAppWidgetIds(thisWidget);
            _ = UpdateAllAsync(context, appWidgetManager, appWidgetIds);
        }
    }

    private static async Task UpdateAllAsync(Context context, AppWidgetManager appWidgetManager, int[] appWidgetIds)
    {
        try
        {
            using var http = new HttpClient();
            var url = Constants.BaseUrl + "/Weather/current"; // Default on server

            // Try last known location if permission granted
            try
            {
                bool hasPermission =
                    context.CheckSelfPermission(global::Android.Manifest.Permission.AccessFineLocation) == Permission.Granted ||
                    context.CheckSelfPermission(global::Android.Manifest.Permission.AccessCoarseLocation) == Permission.Granted;

                if (hasPermission)
                {
                    var lm = (LocationManager?)context.GetSystemService(Context.LocationService);
                    if (lm != null)
                    {
                        ALocation? best = null;
                        foreach (var provider in lm.GetProviders(true))
                        {
                            var loc = lm.GetLastKnownLocation(provider);
                            if (loc != null && (best == null || loc.Time > best.Time))
                                best = loc;
                        }
                        if (best != null)
                        {
                            var lat = best.Latitude.ToString(CultureInfo.InvariantCulture);
                            var lon = best.Longitude.ToString(CultureInfo.InvariantCulture);
                            url = url + "?lat=" + lat + "&lon=" + lon;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Info(LogTag, $"Location not used for widget: {ex.Message}");
            }

            var data = await http.GetFromJsonAsync<List<WeatherData>>(url);
            var first = data?.FirstOrDefault();

            foreach (var id in appWidgetIds)
            {
                var views = new RemoteViews(context.PackageName, global::HanyFokVan.Mobile.Resource.Layout.weather_widget_detailed);

                if (first != null)
                {
                    views.SetTextViewText(global::HanyFokVan.Mobile.Resource.Id.txtTempDetailed, $"{first.TemperatureC:F1}°C");

                    // Humidity with label
                    string humidityText = first.Humidity.HasValue
                        ? $"Humidity: {first.Humidity.Value:F0}%"
                        : "";
                    views.SetTextViewText(global::HanyFokVan.Mobile.Resource.Id.txtHumidityDetailed, humidityText);

                    // Pressure with label
                    string pressureText = first.PressureMb.HasValue
                        ? $"Pressure: {first.PressureMb.Value:F1} mb"
                        : "";
                    views.SetTextViewText(global::HanyFokVan.Mobile.Resource.Id.txtPressureDetailed, pressureText);

                    views.SetTextViewText(global::HanyFokVan.Mobile.Resource.Id.txtLocationDetailed, first.Location ?? "");
                }
                else
                {
                    views.SetTextViewText(global::HanyFokVan.Mobile.Resource.Id.txtTempDetailed, "–°C");
                    views.SetTextViewText(global::HanyFokVan.Mobile.Resource.Id.txtHumidityDetailed, "");
                    views.SetTextViewText(global::HanyFokVan.Mobile.Resource.Id.txtPressureDetailed, "");
                    views.SetTextViewText(global::HanyFokVan.Mobile.Resource.Id.txtLocationDetailed, "Nincs adat");
                }

                // Tap → broadcast refresh (no app open)
                var refreshIntent = new Intent(context, typeof(WeatherWidgetDetailedProvider));
                refreshIntent.SetAction(ACTION_REFRESH);
                var pendingFlags = PendingIntentFlags.UpdateCurrent | (Build.VERSION.SdkInt >= BuildVersionCodes.M ? PendingIntentFlags.Immutable : 0);
                var pending = PendingIntent.GetBroadcast(context, id, refreshIntent, pendingFlags);
                views.SetOnClickPendingIntent(global::HanyFokVan.Mobile.Resource.Id.widgetDetailedRoot, pending);

                appWidgetManager.UpdateAppWidget(id, views);
            }
        }
        catch (Exception ex)
        {
            Log.Warn(LogTag, $"Widget update failed: {ex.Message}");
        }
    }
}
#endif
