using System.Globalization;
using ZXing.Net.Maui;

namespace ZesTour.Views;

public partial class StoreRegistrationQrPage : ContentPage
{
    private readonly string _qrValue;

    public StoreRegistrationQrPage(string storeName, string address, double latitude, double longitude)
    {
        InitializeComponent();

        StoreNameLabel.Text = string.IsNullOrWhiteSpace(storeName) ? "Cửa hàng" : storeName.Trim();
        StoreAddressLabel.Text = string.IsNullOrWhiteSpace(address) ? "Chưa có địa chỉ" : address.Trim();

        _qrValue = BuildMapsUrl(latitude, longitude);
        StoreQrCodeView.Format = BarcodeFormat.QrCode;
        StoreQrCodeView.Value = _qrValue;
    }

    private static string BuildMapsUrl(double latitude, double longitude)
    {
        var lat = latitude.ToString("0.######", CultureInfo.InvariantCulture);
        var lng = longitude.ToString("0.######", CultureInfo.InvariantCulture);
        return $"https://www.google.com/maps/search/?api=1&query={lat},{lng}";
    }

    private async void OnOpenMapsClicked(object? sender, EventArgs e)
    {
        if (Uri.TryCreate(_qrValue, UriKind.Absolute, out var uri))
        {
            await Launcher.Default.OpenAsync(uri);
        }
    }

    private async void OnCopyLinkClicked(object? sender, EventArgs e)
    {
        await Clipboard.Default.SetTextAsync(_qrValue);
        await DisplayAlertAsync("Đã sao chép", "Liên kết chỉ đường đã được sao chép.", "OK");
    }

    private async void OnDoneClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        await Navigation.PopAsync();
    }
}
