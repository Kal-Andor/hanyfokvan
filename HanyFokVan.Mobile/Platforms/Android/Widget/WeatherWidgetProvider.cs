﻿#if ANDROID
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
[MetaData(AppWidgetManager.MetaDataAppwidgetProvider, Resource = "@xml/weather_widget_info")]
public class WeatherWidgetProvider : AppWidgetProvider
{
    public const string ACTION_REFRESH = "com.companyname.hanyfokvan.WIDGET_REFRESH";
    private const string LogTag = "WeatherWidget";

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
            var thisWidget = new ComponentName(context, Java.Lang.Class.FromType(typeof(WeatherWidgetProvider)).Name);
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
                var views = new RemoteViews(context.PackageName, global::HanyFokVan.Mobile.Resource.Layout.weather_widget);

                if (first != null)
                {
                    views.SetTextViewText(global::HanyFokVan.Mobile.Resource.Id.txtTemp, $"{first.TemperatureC:F1}°C");
                    views.SetTextViewText(global::HanyFokVan.Mobile.Resource.Id.txtLocation, first.Location ?? "");
                }
                else
                {
                    views.SetTextViewText(global::HanyFokVan.Mobile.Resource.Id.txtTemp, "–°C");
                    views.SetTextViewText(global::HanyFokVan.Mobile.Resource.Id.txtLocation, "Nincs adat");
                }

                // Tap → broadcast refresh (no app open)
                var refreshIntent = new Intent(context, typeof(WeatherWidgetProvider));
                refreshIntent.SetAction(ACTION_REFRESH);
                var pendingFlags = PendingIntentFlags.UpdateCurrent | (Build.VERSION.SdkInt >= BuildVersionCodes.M ? PendingIntentFlags.Immutable : 0);
                var pending = PendingIntent.GetBroadcast(context, id, refreshIntent, pendingFlags);
                views.SetOnClickPendingIntent(global::HanyFokVan.Mobile.Resource.Id.widgetRoot, pending);

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
