using System.Text.Json;
using Microsoft.Maui.Storage;

namespace ZesTour.Views;

public partial class SettingsPage : ContentPage
{
    private const string SettingsKey = "zes_settings_v1";

    public SettingsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
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
            NotificationEnabled = NotificationSwitch.IsToggled
        };

        Preferences.Default.Set(SettingsKey, JsonSerializer.Serialize(data));
        await DisplayAlertAsync("Đã lưu", "Cài đặt của bạn đã được cập nhật.", "OK");
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
    }
}
