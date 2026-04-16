using System.Globalization;
using System.Text.Json;
using Microsoft.Maui.Storage;

namespace MobileApp.Services;

public sealed class AppLanguageService
{
    private const string SettingsKey = "zes_settings_v1";
    private static readonly HashSet<string> SupportedLanguageCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "vi", "en", "ja", "ko", "zh"
    };

    public string CurrentInterfaceLanguageCode { get; private set; } = "vi";

    public void InitializeFromSettings()
    {
        var settings = LoadSettings();

        if (settings is null)
        {
            settings = new AppSettingData
            {
                InterfaceLanguageCode = "vi"
            };
            SaveSettings(settings);
        }

        if (string.Equals(NormalizeLanguageCode(settings.InterfaceLanguageCode), "auto", StringComparison.OrdinalIgnoreCase))
        {
            settings.InterfaceLanguageCode = "vi";
            SaveSettings(settings);
        }

        ApplyInterfaceLanguage(settings.InterfaceLanguageCode);
    }

    public bool IsInterfaceLanguageChanged(string languageCode)
    {
        var resolved = ResolveLanguage(languageCode);
        return !string.Equals(CurrentInterfaceLanguageCode, resolved, StringComparison.OrdinalIgnoreCase);
    }

    public void ApplyInterfaceLanguage(string languageCode)
    {
        var resolved = ResolveLanguage(languageCode);
        CurrentInterfaceLanguageCode = resolved;

        var culture = CreateCulture(resolved);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public string ResolveLanguage(string? languageCode)
    {
        var normalized = NormalizeLanguageCode(languageCode);
        if (string.Equals(normalized, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return "vi";
        }

        return EnsureSupported(normalized);
    }

    private static string ResolveDeviceLanguage()
    {
        var deviceCode = NormalizeLanguageCode(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
        return EnsureSupported(deviceCode);
    }

    private static string EnsureSupported(string normalized)
    {
        return SupportedLanguageCodes.Contains(normalized) ? normalized : "vi";
    }

    private static CultureInfo CreateCulture(string languageCode)
    {
        var cultureName = languageCode switch
        {
            "en" => "en-US",
            "ja" => "ja-JP",
            "ko" => "ko-KR",
            "zh" => "zh-CN",
            _ => "vi-VN"
        };

        return new CultureInfo(cultureName);
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return "auto";
        }

        var normalized = languageCode.Trim().ToLowerInvariant();
        var delimiter = normalized.IndexOfAny(new[] { '-', '_' });
        if (delimiter > 0)
        {
            normalized = normalized[..delimiter];
        }

        return normalized switch
        {
            "vn" => "vi",
            _ => normalized
        };
    }

    private static AppSettingData? LoadSettings()
    {
        var json = Preferences.Default.Get(SettingsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettingData>(json);
        }
        catch
        {
            return null;
        }
    }

    private static void SaveSettings(AppSettingData settings)
    {
        Preferences.Default.Set(SettingsKey, JsonSerializer.Serialize(settings));
    }

    private sealed class AppSettingData
    {
        public bool AutoPlay { get; set; }
        public bool BatteryOptimized { get; set; }
        public bool NotificationEnabled { get; set; }
        public bool StoreNarrationEnabled { get; set; } = true;
        public string GpsSensitivityCode { get; set; } = "balanced";
        public double PoiRadiusScale { get; set; } = 1.0;
        public string TtsVoiceCode { get; set; } = "auto";
        public string NarrationLanguageCode { get; set; } = "auto";
        public string InterfaceLanguageCode { get; set; } = "vi";
    }
}
