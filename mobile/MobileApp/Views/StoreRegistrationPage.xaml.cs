using MobileApp.Services;
using System.IO;
using System.Text.Json;

namespace ZesTour.Views;

public partial class StoreRegistrationPage : ContentPage
{
    private readonly ApiService _apiService;
    private readonly AuthService _authService;
    private readonly AppNavigator _navigator;
    private byte[]? _selectedImageBytes;
    private string? _selectedImageFileName;
    private string? _selectedImageContentType;
    private bool _isRestoringDraft;

    public StoreRegistrationPage(ApiService apiService, AuthService authService, AppNavigator navigator)
    {
        _apiService = apiService;
        _authService = authService;
        _navigator = navigator;
        InitializeComponent();

        var userName = _authService.CurrentUser?.Name?.Trim();
        if (!string.IsNullOrWhiteSpace(userName))
        {
            OwnerNameEntry.Text = userName;
        }

        StoreNameEntry.TextChanged += OnDraftFieldChanged;
        OwnerNameEntry.TextChanged += OnDraftFieldChanged;
        PhoneEntry.TextChanged += OnDraftFieldChanged;
        AddressEntry.TextChanged += OnDraftFieldChanged;
        CategoryEntry.TextChanged += OnDraftFieldChanged;
        DescriptionEditor.TextChanged += OnDraftFieldChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!string.Equals(_authService.CurrentUser?.Role, "seller", StringComparison.OrdinalIgnoreCase))
        {
            await DisplayAlertAsync("Không có quyền", "Chỉ tài khoản Người bán mới có thể đăng ký cửa hàng.", "OK");
            await Navigation.PopAsync();
            return;
        }

        RestoreDraft();
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnSubmitClicked(object? sender, EventArgs e)
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

        string? imageUrl = null;
        if (_selectedImageBytes is not null && _selectedImageBytes.Length > 0)
        {
            var upload = await _apiService.UploadStoreImageAsync(
                _selectedImageBytes,
                _selectedImageFileName ?? "store.jpg",
                _selectedImageContentType);
            if (!upload.Success)
            {
                MessageLabel.Text = upload.Message;
                return;
            }

            imageUrl = upload.ImageUrl;
        }

        var result = await _apiService.SubmitStoreRegistrationAsync(
            storeName,
            ownerName,
            phone,
            address,
            category,
            description,
            imageUrl);

        if (!result.Success)
        {
            MessageLabel.Text = result.Message;
            return;
        }

        await DisplayAlertAsync("Gửi thành công", result.Message, "OK");
        ClearDraft();
        await _navigator.ShowStoreManagementAsync();
    }

    private async void OnPickImageClicked(object? sender, EventArgs e)
    {
        MessageLabel.Text = string.Empty;

        try
        {
            var photo = await MediaPicker.Default.PickPhotoAsync();
            if (photo is null)
            {
                return;
            }

            await using var stream = await photo.OpenReadAsync();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);

            _selectedImageBytes = ms.ToArray();
            _selectedImageFileName = photo.FileName;
            _selectedImageContentType = photo.ContentType;

            SelectedImageLabel.Text = $"Đã chọn: {photo.FileName}";
            StoreImagePreview.Source = ImageSource.FromStream(() => new MemoryStream(_selectedImageBytes));
            StoreImagePreview.IsVisible = true;
        }
        catch (Exception ex)
        {
            MessageLabel.Text = "Không thể chọn ảnh. Vui lòng thử lại.";
            Console.WriteLine($"Lỗi chọn ảnh: {ex.Message}");
        }
    }

    private void OnDraftFieldChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isRestoringDraft)
        {
            return;
        }

        SaveDraft();
    }

    private void SaveDraft()
    {
        var draft = new StoreRegistrationDraft
        {
            StoreName = StoreNameEntry.Text?.Trim() ?? string.Empty,
            OwnerName = OwnerNameEntry.Text?.Trim() ?? string.Empty,
            Phone = PhoneEntry.Text?.Trim() ?? string.Empty,
            Address = AddressEntry.Text?.Trim() ?? string.Empty,
            Category = CategoryEntry.Text?.Trim() ?? string.Empty,
            Description = DescriptionEditor.Text?.Trim() ?? string.Empty,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(draft);
        Preferences.Default.Set(GetDraftKey(), json);
    }

    private void RestoreDraft()
    {
        var json = Preferences.Default.Get(GetDraftKey(), string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            var draft = JsonSerializer.Deserialize<StoreRegistrationDraft>(json);
            if (draft is null)
            {
                return;
            }

            _isRestoringDraft = true;
            StoreNameEntry.Text = string.IsNullOrWhiteSpace(StoreNameEntry.Text) ? draft.StoreName : StoreNameEntry.Text;
            OwnerNameEntry.Text = string.IsNullOrWhiteSpace(OwnerNameEntry.Text) ? draft.OwnerName : OwnerNameEntry.Text;
            PhoneEntry.Text = string.IsNullOrWhiteSpace(PhoneEntry.Text) ? draft.Phone : PhoneEntry.Text;
            AddressEntry.Text = string.IsNullOrWhiteSpace(AddressEntry.Text) ? draft.Address : AddressEntry.Text;
            CategoryEntry.Text = string.IsNullOrWhiteSpace(CategoryEntry.Text) ? draft.Category : CategoryEntry.Text;
            DescriptionEditor.Text = string.IsNullOrWhiteSpace(DescriptionEditor.Text) ? draft.Description : DescriptionEditor.Text;
            _isRestoringDraft = false;

            if (!string.IsNullOrWhiteSpace(draft.StoreName) ||
                !string.IsNullOrWhiteSpace(draft.Phone) ||
                !string.IsNullOrWhiteSpace(draft.Address))
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlertAsync("Khôi phục bản nháp", "Đã tự khôi phục thông tin đăng ký cửa hàng chưa gửi xong.", "OK");
                });
            }
        }
        catch
        {
            _isRestoringDraft = false;
        }
    }

    private void ClearDraft()
    {
        Preferences.Default.Remove(GetDraftKey());
    }

    private string GetDraftKey()
    {
        var userId = _authService.CurrentUser?.Id ?? 0;
        return $"zestour_store_registration_draft_{userId}";
    }

    private sealed class StoreRegistrationDraft
    {
        public string StoreName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime UpdatedAtUtc { get; set; }
    }
}
