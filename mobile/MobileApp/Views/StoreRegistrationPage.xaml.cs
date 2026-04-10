using System.Text.Json;
using Microsoft.Maui.Storage;

namespace ZesTour.Views;

public partial class StoreRegistrationPage : ContentPage
{
    private const string StoreRegistrationsKey = "zes_store_registrations_v1";

    public StoreRegistrationPage()
    {
        InitializeComponent();
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnSubmitClicked(object sender, EventArgs e)
    {
        MessageLabel.Text = string.Empty;

        var storeName = StoreNameEntry.Text?.Trim() ?? string.Empty;
        var ownerName = OwnerNameEntry.Text?.Trim() ?? string.Empty;
        var phone = PhoneEntry.Text?.Trim() ?? string.Empty;
        var address = AddressEntry.Text?.Trim() ?? string.Empty;
        var category = CategoryEntry.Text?.Trim() ?? string.Empty;
        var description = DescriptionEditor.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(storeName) ||
            string.IsNullOrWhiteSpace(ownerName) ||
            string.IsNullOrWhiteSpace(phone) ||
            string.IsNullOrWhiteSpace(address))
        {
            MessageLabel.Text = "Vui lòng nhập đủ thông tin bắt buộc.";
            return;
        }

        var registrations = LoadRegistrations();
        registrations.Add(new StoreRegistrationRequest
        {
            StoreName = storeName,
            OwnerName = ownerName,
            Phone = phone,
            Address = address,
            Category = category,
            Description = description,
            SubmittedAtUtc = DateTime.UtcNow
        });

        SaveRegistrations(registrations);

        await DisplayAlertAsync("Gửi thành công", "Yêu cầu đăng ký cửa hàng đã được ghi nhận.", "OK");
        await Navigation.PopAsync();
    }

    private static List<StoreRegistrationRequest> LoadRegistrations()
    {
        var json = Preferences.Default.Get(StoreRegistrationsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<StoreRegistrationRequest>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<StoreRegistrationRequest>>(json) ?? new List<StoreRegistrationRequest>();
        }
        catch
        {
            return new List<StoreRegistrationRequest>();
        }
    }

    private static void SaveRegistrations(List<StoreRegistrationRequest> registrations)
    {
        var json = JsonSerializer.Serialize(registrations);
        Preferences.Default.Set(StoreRegistrationsKey, json);
    }

    private sealed class StoreRegistrationRequest
    {
        public string StoreName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime SubmittedAtUtc { get; set; }
    }
}
