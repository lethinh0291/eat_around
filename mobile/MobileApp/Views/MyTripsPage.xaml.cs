using System.Text.Json;
using Microsoft.Maui.Storage;
using MobileApp.Resources.Localization;
using System.Globalization;

namespace ZesTour.Views;

public partial class MyTripsPage : ContentPage
{
    private const string TripsKey = "zes_trip_history_v1";

    public MyTripsPage()
    {
        InitializeComponent();
        ApplyLocalizedText();
    }

    private void ApplyLocalizedText()
    {
        TripsTitleLabel.Text = AppText.Get("Trips_Title");
        TripsSubtitleLabel.Text = AppText.Get("Trips_Subtitle");
        TripsHintLabel.Text = AppText.Get("Trips_Hint");
        ClearSelectedTripButton.Text = AppText.Get("Trips_ClearSelected");
        EmptyLabel.Text = AppText.Get("Trips_Empty");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshTripsUi();
    }

    private void RefreshTripsUi()
    {
        var items = LoadSelectedTrip();
        TripsCollectionView.ItemsSource = items;
        EmptyLabel.IsVisible = items.Count == 0;
        ClearSelectedTripButton.IsVisible = items.Count > 0;
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnClearSelectedTripClicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlertAsync(
            AppText.Get("Trips_ClearTitle"),
            AppText.Get("Trips_ClearConfirm"),
            AppText.Get("Trips_Delete"),
            AppText.Get("Trips_Cancel"));

        if (!confirm)
        {
            return;
        }

        Preferences.Default.Remove(TripsKey);
        RefreshTripsUi();
    }

    private static List<TripItemViewModel> LoadSelectedTrip()
    {
        var json = Preferences.Default.Get(TripsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<TripItemViewModel>();
        }

        try
        {
            var singleItem = JsonSerializer.Deserialize<TripItem>(json);
            if (singleItem is not null)
            {
                return new List<TripItemViewModel>
                {
                    ToViewModel(singleItem)
                };
            }

            var legacyItems = JsonSerializer.Deserialize<List<TripItem>>(json) ?? new List<TripItem>();
            var latest = legacyItems
                .OrderByDescending(item => item.CreatedAtUtc)
                .FirstOrDefault();

            return latest is null
                ? new List<TripItemViewModel>()
                : new List<TripItemViewModel> { ToViewModel(latest) };
        }
        catch
        {
            return new List<TripItemViewModel>();
        }
    }

    private static TripItemViewModel ToViewModel(TripItem item)
    {
        return new TripItemViewModel
        {
            Name = string.IsNullOrWhiteSpace(item.Name) ? AppText.Get("Trips_DefaultName") : item.Name,
            Description = string.IsNullOrWhiteSpace(item.Description) ? AppText.Get("Trips_DefaultDescription") : item.Description,
            CreatedAtText = item.CreatedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
        };
    }

    private sealed class TripItem
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }

    private sealed class TripItemViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CreatedAtText { get; set; } = string.Empty;
    }
}
