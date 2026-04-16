using System.Text.Json;
using Microsoft.Maui.Storage;
using MobileApp.Resources.Localization;
using MobileApp.Services;

namespace ZesTour.Views;

public partial class SettingsPage : ContentPage
{
    private const string SettingsKey = "zes_settings_v1";
    private readonly AppLanguageService _appLanguageService;
    private readonly AppNavigator _navigator;
    private List<LanguageOption> _interfaceLanguageOptions = new();
    private List<LanguageOption> _narrationLanguageOptions = new();
    private List<GpsSensitivityOption> _gpsSensitivityOptions = new();
    private List<PoiRadiusOption> _poiRadiusOptions = new();
    private List<TtsVoiceOption> _ttsVoiceOptions = new();
    private bool _ttsVoicesLoaded;
    private string _savedTtsVoiceCode = "auto";
    private string _savedGpsSensitivityCode = "balanced";
    private double _savedPoiRadiusScale = 1.0;

    public SettingsPage(AppLanguageService appLanguageService, AppNavigator navigator)
    {
        _appLanguageService = appLanguageService;
        _navigator = navigator;
        InitializeComponent();
        BuildLanguageOptions();
        BuildGpsSensitivityOptions();
        BuildPoiRadiusOptions();
        BuildDefaultTtsVoiceOptions();
        ConfigurePickers();
        ApplyLocalizedText();
        LoadSettings();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await EnsureTtsVoiceOptionsAsync();
    }

    private void BuildLanguageOptions()
    {
        var deviceLanguageCode = _appLanguageService.ResolveLanguage("auto");
        var deviceLanguageDisplay = GetLanguageNameByCode(deviceLanguageCode);
        var autoLabel = AppText.Format("Settings_AutoDeviceDetail", deviceLanguageDisplay);

        _interfaceLanguageOptions = new List<LanguageOption>
        {
            new(autoLabel, "auto"),
            new(AppText.Get("Language_Vietnamese"), "vi"),
            new(AppText.Get("Language_English"), "en"),
            new(AppText.Get("Language_Japanese"), "ja"),
            new(AppText.Get("Language_Korean"), "ko"),
            new(AppText.Get("Language_Chinese"), "zh")
        };

        _narrationLanguageOptions = new List<LanguageOption>(_interfaceLanguageOptions);
    }

    private void BuildGpsSensitivityOptions()
    {
        _gpsSensitivityOptions = new List<GpsSensitivityOption>
        {
            new(AppText.Get("Settings_GpsSensitivity_Balanced"), "balanced"),
            new(AppText.Get("Settings_GpsSensitivity_High"), "high"),
            new(AppText.Get("Settings_GpsSensitivity_Battery"), "battery")
        };
    }

    private void BuildPoiRadiusOptions()
    {
        _poiRadiusOptions = new List<PoiRadiusOption>
        {
            new(AppText.Get("Settings_PoiRadius_Compact"), 0.8),
            new(AppText.Get("Settings_PoiRadius_Normal"), 1.0),
            new(AppText.Get("Settings_PoiRadius_Wide"), 1.3)
        };
    }

    private void BuildDefaultTtsVoiceOptions()
    {
        _ttsVoiceOptions = new List<TtsVoiceOption>
        {
            new(AppText.Get("Settings_TtsVoiceAuto"), "auto")
        };
    }

    private static string GetLanguageNameByCode(string languageCode)
    {
        return NormalizeLanguageCode(languageCode) switch
        {
            "en" => AppText.Get("Language_English"),
            "ja" => AppText.Get("Language_Japanese"),
            "ko" => AppText.Get("Language_Korean"),
            "zh" => AppText.Get("Language_Chinese"),
            _ => AppText.Get("Language_Vietnamese")
        };
    }

    private void ConfigurePickers()
    {
        InterfaceLanguagePicker.ItemsSource = _interfaceLanguageOptions;
        InterfaceLanguagePicker.ItemDisplayBinding = new Binding(nameof(LanguageOption.DisplayName));
        NarrationLanguagePicker.ItemsSource = _narrationLanguageOptions;
        NarrationLanguagePicker.ItemDisplayBinding = new Binding(nameof(LanguageOption.DisplayName));

        GpsSensitivityPicker.ItemsSource = _gpsSensitivityOptions;
        GpsSensitivityPicker.ItemDisplayBinding = new Binding(nameof(GpsSensitivityOption.DisplayName));

        PoiRadiusPicker.ItemsSource = _poiRadiusOptions;
        PoiRadiusPicker.ItemDisplayBinding = new Binding(nameof(PoiRadiusOption.DisplayName));

        TtsVoicePicker.ItemsSource = _ttsVoiceOptions;
        TtsVoicePicker.ItemDisplayBinding = new Binding(nameof(TtsVoiceOption.DisplayName));
    }

    private void ApplyLocalizedText()
    {
        TitleLabel.Text = AppText.Get("Settings_Title");
        SubtitleLabel.Text = AppText.Get("Settings_Subtitle");
        StorageHintLabel.Text = AppText.Get("Settings_LocalStorageHint");
        SystemOptionsLabel.Text = AppText.Get("Settings_SystemOptions");
        AutoPlayLabel.Text = AppText.Get("Settings_AutoPlay");
        BatteryLabel.Text = AppText.Get("Settings_Battery");
        NotificationLabel.Text = AppText.Get("Settings_Notifications");
        StoreNarrationLabel.Text = AppText.Get("Settings_StoreNarration");
        GpsSensitivityLabel.Text = AppText.Get("Settings_GpsSensitivity");
        PoiRadiusLabel.Text = AppText.Get("Settings_PoiRadius");
        TtsVoiceLabel.Text = AppText.Get("Settings_TtsVoice");
        InterfaceLanguageLabel.Text = AppText.Get("Settings_InterfaceLanguage");
        NarrationLanguageLabel.Text = AppText.Get("Settings_NarrationLanguage");

        var pickerTitle = AppText.Get("Settings_SelectLanguage");
        InterfaceLanguagePicker.Title = pickerTitle;
        NarrationLanguagePicker.Title = pickerTitle;
        var selectOptionTitle = AppText.Get("Settings_SelectOption");
        GpsSensitivityPicker.Title = selectOptionTitle;
        PoiRadiusPicker.Title = selectOptionTitle;
        TtsVoicePicker.Title = selectOptionTitle;
        SaveButton.Text = AppText.Get("Settings_Save");
    }

    private void LoadSettings()
    {
        InterfaceLanguagePicker.SelectedItem = _interfaceLanguageOptions[0];
        NarrationLanguagePicker.SelectedItem = _narrationLanguageOptions[0];
        GpsSensitivityPicker.SelectedItem = _gpsSensitivityOptions[0];
        PoiRadiusPicker.SelectedItem = _poiRadiusOptions.FirstOrDefault(option => Math.Abs(option.Scale - 1.0) < 0.001) ?? _poiRadiusOptions[0];
        TtsVoicePicker.SelectedItem = _ttsVoiceOptions[0];

        var json = Preferences.Default.Get(SettingsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            var data = JsonSerializer.Deserialize<SettingData>(json);
            if (data is null)
            {
                return;
            }

            AutoPlaySwitch.IsToggled = data.AutoPlay;
            BatterySwitch.IsToggled = data.BatteryOptimized;
            NotificationSwitch.IsToggled = data.NotificationEnabled;
            StoreNarrationSwitch.IsToggled = data.StoreNarrationEnabled;
            _savedGpsSensitivityCode = NormalizeGpsSensitivityCode(data.GpsSensitivityCode);
            _savedPoiRadiusScale = NormalizePoiRadiusScale(data.PoiRadiusScale);
            _savedTtsVoiceCode = NormalizeTtsVoiceCode(data.TtsVoiceCode);

            GpsSensitivityPicker.SelectedItem = _gpsSensitivityOptions.FirstOrDefault(option =>
                string.Equals(option.Code, _savedGpsSensitivityCode, StringComparison.OrdinalIgnoreCase))
                ?? _gpsSensitivityOptions[0];

            PoiRadiusPicker.SelectedItem = _poiRadiusOptions.FirstOrDefault(option =>
                Math.Abs(option.Scale - _savedPoiRadiusScale) < 0.001)
                ?? _poiRadiusOptions.FirstOrDefault(option => Math.Abs(option.Scale - 1.0) < 0.001)
                ?? _poiRadiusOptions[0];

            InterfaceLanguagePicker.SelectedItem = _interfaceLanguageOptions.FirstOrDefault(option =>
                string.Equals(option.Code, NormalizeLanguageCode(data.InterfaceLanguageCode), StringComparison.OrdinalIgnoreCase))
                ?? _interfaceLanguageOptions[0];

            NarrationLanguagePicker.SelectedItem = _narrationLanguageOptions.FirstOrDefault(option =>
                string.Equals(option.Code, NormalizeLanguageCode(data.NarrationLanguageCode), StringComparison.OrdinalIgnoreCase))
                ?? _narrationLanguageOptions[0];

            TtsVoicePicker.SelectedItem = _ttsVoiceOptions.FirstOrDefault(option =>
                string.Equals(option.Code, _savedTtsVoiceCode, StringComparison.OrdinalIgnoreCase))
                ?? _ttsVoiceOptions[0];
        }
        catch
        {
            // Ignore invalid data.
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var data = new SettingData
        {
            AutoPlay = AutoPlaySwitch.IsToggled,
            BatteryOptimized = BatterySwitch.IsToggled,
            NotificationEnabled = NotificationSwitch.IsToggled,
            StoreNarrationEnabled = StoreNarrationSwitch.IsToggled,
            GpsSensitivityCode = GetSelectedGpsSensitivityCode(),
            PoiRadiusScale = GetSelectedPoiRadiusScale(),
            TtsVoiceCode = GetSelectedTtsVoiceCode(),
            NarrationLanguageCode = GetSelectedNarrationLanguageCode(),
            InterfaceLanguageCode = GetSelectedInterfaceLanguageCode()
        };

        var requiresReload = _appLanguageService.IsInterfaceLanguageChanged(data.InterfaceLanguageCode);
        Preferences.Default.Set(SettingsKey, JsonSerializer.Serialize(data));

        if (requiresReload)
        {
            _appLanguageService.ApplyInterfaceLanguage(data.InterfaceLanguageCode);
            await DisplayAlertAsync(
                AppText.Get("Settings_SaveSuccessTitle"),
                AppText.Get("Settings_SaveAndReloadMessage"),
                AppText.Get("Common_Ok"));
            await _navigator.ShowLoadingAsync();
            return;
        }

        await DisplayAlertAsync(
            AppText.Get("Settings_SaveSuccessTitle"),
            AppText.Get("Settings_SaveSuccessMessage"),
            AppText.Get("Common_Ok"));
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        await Navigation.PopAsync();
    }

    private sealed class SettingData
    {
        public bool AutoPlay { get; set; }
        public bool BatteryOptimized { get; set; }
        public bool NotificationEnabled { get; set; }
        public bool StoreNarrationEnabled { get; set; } = true;
        public string GpsSensitivityCode { get; set; } = "balanced";
        public double PoiRadiusScale { get; set; } = 1.0;
        public string TtsVoiceCode { get; set; } = "auto";
        public string NarrationLanguageCode { get; set; } = "auto";
        public string InterfaceLanguageCode { get; set; } = "auto";
    }

    private string GetSelectedInterfaceLanguageCode()
    {
        if (InterfaceLanguagePicker.SelectedItem is LanguageOption selected)
        {
            return selected.Code;
        }

        return _interfaceLanguageOptions[0].Code;
    }

    private string GetSelectedNarrationLanguageCode()
    {
        if (NarrationLanguagePicker.SelectedItem is LanguageOption selected)
        {
            return selected.Code;
        }

        return _narrationLanguageOptions[0].Code;
    }

    private string GetSelectedGpsSensitivityCode()
    {
        if (GpsSensitivityPicker.SelectedItem is GpsSensitivityOption selected)
        {
            return selected.Code;
        }

        return _gpsSensitivityOptions[0].Code;
    }

    private double GetSelectedPoiRadiusScale()
    {
        if (PoiRadiusPicker.SelectedItem is PoiRadiusOption selected)
        {
            return selected.Scale;
        }

        return 1.0;
    }

    private string GetSelectedTtsVoiceCode()
    {
        if (TtsVoicePicker.SelectedItem is TtsVoiceOption selected)
        {
            return selected.Code;
        }

        return "auto";
    }

    private async Task EnsureTtsVoiceOptionsAsync()
    {
        if (_ttsVoicesLoaded)
        {
            return;
        }

        _ttsVoicesLoaded = true;
        try
        {
            var locales = await TextToSpeech.Default.GetLocalesAsync();
            var options = locales
                .Where(locale => !string.IsNullOrWhiteSpace(locale.Language))
                .GroupBy(locale => locale.Language, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(locale => locale.Name)
                .Select(locale => new TtsVoiceOption($"{locale.Name} ({locale.Language})", locale.Language))
                .ToList();

            _ttsVoiceOptions = new List<TtsVoiceOption>
            {
                new(AppText.Get("Settings_TtsVoiceAuto"), "auto")
            };
            _ttsVoiceOptions.AddRange(options);

            TtsVoicePicker.ItemsSource = _ttsVoiceOptions;
            TtsVoicePicker.SelectedItem = _ttsVoiceOptions.FirstOrDefault(option =>
                string.Equals(option.Code, _savedTtsVoiceCode, StringComparison.OrdinalIgnoreCase))
                ?? _ttsVoiceOptions[0];
        }
        catch
        {
            // Keep only auto option if locale enumeration fails on device.
            _ttsVoiceOptions = new List<TtsVoiceOption>
            {
                new(AppText.Get("Settings_TtsVoiceAuto"), "auto")
            };

            TtsVoicePicker.ItemsSource = _ttsVoiceOptions;
            TtsVoicePicker.SelectedItem = _ttsVoiceOptions[0];
        }
    }

    private static string NormalizeGpsSensitivityCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "balanced";
        }

        var normalized = code.Trim().ToLowerInvariant();
        return normalized switch
        {
            "high" => "high",
            "battery" => "battery",
            _ => "balanced"
        };
    }

    private static double NormalizePoiRadiusScale(double scale)
    {
        if (scale < 0.8)
        {
            return 0.8;
        }

        if (scale > 1.3)
        {
            return 1.3;
        }

        return Math.Round(scale, 1);
    }

    private static string NormalizeTtsVoiceCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "auto";
        }

        return code.Trim();
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

    private sealed record LanguageOption(string DisplayName, string Code);
    private sealed record GpsSensitivityOption(string DisplayName, string Code);
    private sealed record PoiRadiusOption(string DisplayName, double Scale);
    private sealed record TtsVoiceOption(string DisplayName, string Code);
}
