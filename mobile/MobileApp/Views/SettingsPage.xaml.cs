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

    public SettingsPage(AppLanguageService appLanguageService, AppNavigator navigator)
    {
        _appLanguageService = appLanguageService;
        _navigator = navigator;
        InitializeComponent();
        BuildLanguageOptions();
        ConfigurePickers();
        ApplyLocalizedText();
        LoadSettings();
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
        InterfaceLanguageLabel.Text = AppText.Get("Settings_InterfaceLanguage");
        NarrationLanguageLabel.Text = AppText.Get("Settings_NarrationLanguage");

        var pickerTitle = AppText.Get("Settings_SelectLanguage");
        InterfaceLanguagePicker.Title = pickerTitle;
        NarrationLanguagePicker.Title = pickerTitle;
        SaveButton.Text = AppText.Get("Settings_Save");
    }

    private void LoadSettings()
    {
        InterfaceLanguagePicker.SelectedItem = _interfaceLanguageOptions[0];
        NarrationLanguagePicker.SelectedItem = _narrationLanguageOptions[0];

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
            InterfaceLanguagePicker.SelectedItem = _interfaceLanguageOptions.FirstOrDefault(option =>
                string.Equals(option.Code, NormalizeLanguageCode(data.InterfaceLanguageCode), StringComparison.OrdinalIgnoreCase))
                ?? _interfaceLanguageOptions[0];

            NarrationLanguagePicker.SelectedItem = _narrationLanguageOptions.FirstOrDefault(option =>
                string.Equals(option.Code, NormalizeLanguageCode(data.NarrationLanguageCode), StringComparison.OrdinalIgnoreCase))
                ?? _narrationLanguageOptions[0];
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
}
