using MobileApp.Services;
using System.ComponentModel;
using System.IO;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace ZesTour.Views;

public partial class StoreRegistrationPage : ContentPage
{
    private const double DefaultStoreRadiusMeters = 140;
    private readonly ApiService _apiService;
    private readonly AuthService _authService;
    private readonly AppNavigator _navigator;
    private readonly ObservableCollection<SelectedStoreImage> _selectedImages = [];
    private readonly ObservableCollection<AddressSuggestionItem> _addressSuggestions = [];
    private static readonly HttpClient AddressLookupClient = new();
    private CancellationTokenSource? _addressLookupCts;
    private AddressGeoPoint? _selectedAddressGeo;
    private bool _isApplyingAddressSuggestion;
    private bool _isRestoringDraft;

    public StoreRegistrationPage(ApiService apiService, AuthService authService, AppNavigator navigator)
    {
        _apiService = apiService;
        _authService = authService;
        _navigator = navigator;
        InitializeComponent();
        SelectedImagesCollectionView.ItemsSource = _selectedImages;
        AddressSuggestionsCollectionView.ItemsSource = _addressSuggestions;

        if (AddressLookupClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            AddressLookupClient.DefaultRequestHeaders.UserAgent.ParseAdd("ZesTour-Mobile/1.0");
        }

        var userName = _authService.CurrentUser?.Name?.Trim();
        if (!string.IsNullOrWhiteSpace(userName))
        {
            OwnerNameEntry.Text = userName;
        }

        StoreNameEntry.TextChanged += OnDraftFieldChanged;
        OwnerNameEntry.TextChanged += OnDraftFieldChanged;
        PhoneEntry.TextChanged += OnDraftFieldChanged;
        AddressEntry.TextChanged += OnAddressTextChanged;
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

        if (!string.IsNullOrWhiteSpace(AddressEntry.Text))
        {
            _ = TryPreviewAddressAsync(AddressEntry.Text.Trim());
        }
    }

    protected override void OnDisappearing()
    {
        _addressLookupCts?.Cancel();
        base.OnDisappearing();
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

        var resolvedGeo = _selectedAddressGeo ?? await ResolveAddressGeoAsync(address, CancellationToken.None);
        if (resolvedGeo is null)
        {
            MessageLabel.Text = "Không xác định được vị trí địa chỉ. Vui lòng chọn gợi ý phù hợp.";
            return;
        }

        var imageUrls = new List<string>();
        var primaryImageIndex = _selectedImages.ToList().FindIndex(image => image.IsPrimary);
        if (primaryImageIndex < 0 && _selectedImages.Count > 0)
        {
            primaryImageIndex = 0;
            SetPrimaryImage(_selectedImages[0]);
        }

        foreach (var selectedImage in _selectedImages)
        {
            var upload = await _apiService.UploadStoreImageAsync(
                selectedImage.Bytes,
                selectedImage.FileName,
                selectedImage.ContentType);
            if (!upload.Success)
            {
                MessageLabel.Text = upload.Message;
                return;
            }

            if (!string.IsNullOrWhiteSpace(upload.ImageUrl))
            {
                imageUrls.Add(upload.ImageUrl);
            }
        }

        var primaryImageUrl = primaryImageIndex >= 0 && primaryImageIndex < imageUrls.Count
            ? imageUrls[primaryImageIndex]
            : imageUrls.FirstOrDefault();

        var result = await _apiService.SubmitStoreRegistrationAsync(
            storeName,
            ownerName,
            phone,
            address,
            category,
            description,
            imageUrls,
            primaryImageUrl,
            resolvedGeo.Value.Latitude,
            resolvedGeo.Value.Longitude,
            DefaultStoreRadiusMeters);

        if (!result.Success)
        {
            MessageLabel.Text = result.Message;
            return;
        }

        await DisplayAlertAsync("Gửi thành công", result.Message, "OK");
        ClearDraft();
        ClearSelectedImages();
        await _navigator.ShowStoreManagementAsync();
    }

    private async void OnPickImageClicked(object? sender, EventArgs e)
    {
        MessageLabel.Text = string.Empty;

        try
        {
            var photos = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "Chọn nhiều ảnh cho cửa hàng",
                FileTypes = FilePickerFileType.Images
            });

            if (photos is null)
            {
                return;
            }

            foreach (var photo in photos)
            {
                if (photo is null)
                {
                    continue;
                }

                await using var stream = await photo.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);

                var bytes = ms.ToArray();
                _selectedImages.Add(new SelectedStoreImage(
                    Guid.NewGuid(),
                    bytes,
                    photo.FileName,
                    photo.ContentType,
                    ImageSource.FromStream(() => new MemoryStream(bytes))));
            }

            EnsurePrimaryImageExists();
            UpdateSelectedImagesUi();
        }
        catch (Exception ex)
        {
            MessageLabel.Text = "Không thể chọn ảnh. Vui lòng thử lại.";
            Console.WriteLine($"Lỗi chọn ảnh: {ex.Message}");
        }
    }

    private void OnRemoveSelectedImageClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not SelectedStoreImage selectedImage)
        {
            return;
        }

        var wasPrimary = selectedImage.IsPrimary;
        _selectedImages.Remove(selectedImage);
        if (wasPrimary)
        {
            EnsurePrimaryImageExists();
        }
        UpdateSelectedImagesUi();
    }

    private void OnSetSelectedImagePrimaryClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not SelectedStoreImage selectedImage)
        {
            return;
        }

        SetPrimaryImage(selectedImage);
        UpdateSelectedImagesUi();
    }

    private void OnSelectedImageDragStarting(object? sender, DragStartingEventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not SelectedStoreImage selectedImage)
        {
            return;
        }

        e.Data.Properties["selectedImageId"] = selectedImage.Id;
    }

    private void OnSelectedImageDragOver(object? sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private void OnSelectedImageDrop(object? sender, DropEventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not SelectedStoreImage targetImage)
        {
            return;
        }

        if (!e.Data.Properties.TryGetValue("selectedImageId", out var value) || value is not Guid sourceId)
        {
            return;
        }

        var sourceIndex = _selectedImages.ToList().FindIndex(image => image.Id == sourceId);
        var targetIndex = _selectedImages.ToList().FindIndex(image => image.Id == targetImage.Id);

        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
        {
            return;
        }

        _selectedImages.Move(sourceIndex, targetIndex);
        UpdateSelectedImagesUi();
    }

    private void OnDraftFieldChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isRestoringDraft)
        {
            return;
        }

        SaveDraft();
    }

    private async void OnAddressTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isApplyingAddressSuggestion)
        {
            return;
        }

        _selectedAddressGeo = null;

        await LookupAddressSuggestionsAsync(e.NewTextValue);
    }

    private async Task LookupAddressSuggestionsAsync(string? keyword)
    {
        _addressLookupCts?.Cancel();

        var query = keyword?.Trim() ?? string.Empty;
        if (query.Length < 3)
        {
            _addressSuggestions.Clear();
            AddressSuggestionsCollectionView.IsVisible = false;
            return;
        }

        var cts = new CancellationTokenSource();
        _addressLookupCts = cts;

        try
        {
            await Task.Delay(280, cts.Token);
            var suggestions = await SearchAddressSuggestionsAsync(query, cts.Token);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            _addressSuggestions.Clear();
            foreach (var suggestion in suggestions)
            {
                _addressSuggestions.Add(suggestion);
            }

            AddressSuggestionsCollectionView.IsVisible = _addressSuggestions.Count > 0;
        }
        catch (OperationCanceledException)
        {
            // Ignore stale request.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi gợi ý địa chỉ: {ex.Message}");
        }
    }

    private void OnAddressSuggestionSelected(object? sender, SelectionChangedEventArgs e)
    {
        var selected = e.CurrentSelection?.FirstOrDefault() as AddressSuggestionItem;
        if (selected is null)
        {
            return;
        }

        if (sender is CollectionView collectionView)
        {
            collectionView.SelectedItem = null;
        }

        _isApplyingAddressSuggestion = true;
        AddressEntry.Text = selected.DisplayName;
        _isApplyingAddressSuggestion = false;
        _selectedAddressGeo = new AddressGeoPoint(selected.Latitude, selected.Longitude);

        _addressSuggestions.Clear();
        AddressSuggestionsCollectionView.IsVisible = false;

        ShowAddressPreviewMap(selected.Latitude, selected.Longitude, selected.DisplayName);
    }

    private async Task TryPreviewAddressAsync(string address)
    {
        try
        {
            var geo = await ResolveAddressGeoAsync(address, CancellationToken.None);
            if (geo is null)
            {
                return;
            }

            ShowAddressPreviewMap(geo.Value.Latitude, geo.Value.Longitude, address);
        }
        catch
        {
            // Ignore map preview errors to keep form responsive.
        }
    }

    private async Task<AddressGeoPoint?> ResolveAddressGeoAsync(string address, CancellationToken cancellationToken)
    {
        var suggestions = await SearchAddressSuggestionsAsync(address, cancellationToken);
        var first = suggestions.FirstOrDefault();
        return first is null ? null : new AddressGeoPoint(first.Latitude, first.Longitude);
    }

    private void ShowAddressPreviewMap(double lat, double lng, string title)
    {
        AddressMapPreviewContainer.IsVisible = true;
        AddressMapPreviewLabel.Text = $"Vị trí địa chỉ: {title}";

        var safeTitle = EscapeJavaScript(title);
        var latText = lat.ToString(CultureInfo.InvariantCulture);
        var lngText = lng.ToString(CultureInfo.InvariantCulture);

        AddressMapPreview.Source = new HtmlWebViewSource
        {
            Html = $@"<!DOCTYPE html>
<html>
<head>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no' />
    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
    <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
    <style>
        html, body, #map {{ margin:0; padding:0; width:100%; height:100%; }}
        body {{ overflow:hidden; }}
    </style>
</head>
<body>
    <div id='map'></div>
    <script>
        const lat = {latText};
        const lng = {lngText};
        const map = L.map('map', {{ zoomControl: false }}).setView([lat, lng], 16);
        L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
            attribution: '&copy; OpenStreetMap contributors'
        }}).addTo(map);
        L.marker([lat, lng]).addTo(map).bindPopup('{safeTitle}').openPopup();
    </script>
</body>
</html>"
        };
    }

    private static async Task<List<AddressSuggestionItem>> SearchAddressSuggestionsAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var encoded = Uri.EscapeDataString(query);
        var endpoint = $"https://nominatim.openstreetmap.org/search?format=jsonv2&addressdetails=1&limit=6&countrycodes=vn&q={encoded}";

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        using var response = await AddressLookupClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<List<NominatimAddressResult>>(stream, cancellationToken: cancellationToken) ?? [];

        return payload
            .Select(result =>
            {
                if (!double.TryParse(result.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
                    !double.TryParse(result.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var lng))
                {
                    return null;
                }

                return new AddressSuggestionItem
                {
                    DisplayName = result.DisplayName,
                    Latitude = lat,
                    Longitude = lng
                };
            })
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();
    }

    private static string EscapeJavaScript(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\r", " ")
            .Replace("\n", " ");
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

    private void ClearSelectedImages()
    {
        _selectedImages.Clear();
        UpdateSelectedImagesUi();
    }

    private void EnsurePrimaryImageExists()
    {
        if (_selectedImages.Count == 0)
        {
            return;
        }

        if (_selectedImages.Any(image => image.IsPrimary))
        {
            return;
        }

        SetPrimaryImage(_selectedImages[0]);
    }

    private void SetPrimaryImage(SelectedStoreImage selectedImage)
    {
        foreach (var image in _selectedImages)
        {
            image.IsPrimary = ReferenceEquals(image, selectedImage);
        }
    }

    private void UpdateSelectedImagesUi()
    {
        SelectedImageLabel.Text = _selectedImages.Count == 0
            ? "Chưa chọn ảnh"
            : $"Đã chọn: {_selectedImages.Count} ảnh";

        SelectedImagesCollectionView.IsVisible = _selectedImages.Count > 0;
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

    private sealed class SelectedStoreImage
        : INotifyPropertyChanged
    {
        private bool _isPrimary;

        public SelectedStoreImage(Guid id, byte[] bytes, string fileName, string? contentType, ImageSource previewSource)
        {
            Id = id;
            Bytes = bytes;
            FileName = fileName;
            ContentType = contentType;
            PreviewSource = previewSource;
        }

        public Guid Id { get; }
        public byte[] Bytes { get; }
        public string FileName { get; }
        public string? ContentType { get; }
        public ImageSource PreviewSource { get; }

        public bool IsPrimary
        {
            get => _isPrimary;
            set
            {
                if (_isPrimary == value)
                {
                    return;
                }

                _isPrimary = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPrimary)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class AddressSuggestionItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    private readonly record struct AddressGeoPoint(double Latitude, double Longitude);

    private sealed class NominatimAddressResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("lat")]
        public string Lat { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("lon")]
        public string Lon { get; set; } = string.Empty;
    }
}
