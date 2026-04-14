using System.Globalization;
using System.Resources;

namespace MobileApp.Resources.Localization;

public static class AppText
{
    private static readonly ResourceManager ResourceManager =
        new("MobileApp.Resources.Localization.AppResources", typeof(AppText).Assembly);

    public static string Get(string key)
    {
        var value = ResourceManager.GetString(key, CultureInfo.CurrentUICulture);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    public static string Format(string key, params object[] args)
    {
        var format = Get(key);
        return string.Format(CultureInfo.CurrentCulture, format, args);
    }
}
