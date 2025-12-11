namespace HanyFokVan.Mobile;
public static class Constants
{
    // TODO: Update with your actual Render.com URL
    public const string ProductionUrl = "https://your-app-name.onrender.com";

    public static string BaseUrl
    {
        get
        {
#if DEBUG
            if (DeviceInfo.Platform == DevicePlatform.Android)
                return "http://10.0.2.2:5286";
            
            return "http://localhost:5286";
#else
            return ProductionUrl;
#endif
        }
    }
}
