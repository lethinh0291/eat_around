using System.Text.Json;
using Microsoft.Maui.Storage;

namespace ZesTour.Views;

public partial class MyTripsPage : ContentPage
{
    private const string TripsKey = "zes_trip_history_v1";

    public MyTripsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var items = LoadTrips();
        TripsCollectionView.ItemsSource = items;
        EmptyLabel.IsVisible = items.Count == 0;
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        await Navigation.PopAsync();
    }

    private static List<TripItemViewModel> LoadTrips()
    {
        var json = Preferences.Default.Get(TripsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<TripItemViewModel>();
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<TripItem>>(json) ?? new List<TripItem>();
            return items
                .OrderByDescending(item => item.CreatedAtUtc)
                .Select(item => new TripItemViewModel
                {
                    Name = string.IsNullOrWhiteSpace(item.Name) ? "Điểm đã chọn" : item.Name,
                    Description = string.IsNullOrWhiteSpace(item.Description) ? "Không có mô tả" : item.Description,
                    CreatedAtText = item.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
                })
                .ToList();
        }
        catch
        {
            return new List<TripItemViewModel>();
        }
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
