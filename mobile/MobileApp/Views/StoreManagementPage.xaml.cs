using MobileApp.Services;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace ZesTour.Views;

public partial class StoreManagementPage : ContentPage
{
    private const double DefaultStoreRadiusMeters = 140;
    private static readonly HttpClient AddressLookupClient = new();

    private readonly ApiService _apiService;
    private readonly AuthService _authService;
    private readonly ObservableCollection<ImagePreviewItem> _previewItems = new();
    private readonly ObservableCollection<AddressSuggestionItem> _addressSuggestions = new();

    private List<ApiService.ManagementStoreRegistration> _items = new();
    private ApiService.ManagementStoreRegistration? _selected;
    private string? _primaryImageUrl;
    private AddressGeoPoint? _selectedAddressGeo;
    private CancellationTokenSource? _addressLookupCts;
    private bool _isApplyingAddressSuggestion;

    public StoreManagementPage(ApiService apiService, AuthService authService)
    {
        _apiService = apiService;
        _authService = authService;

        InitializeComponent();

        StoreImagePreviewCollection.ItemsSource = _previewItems;
        AddressSuggestionsCollectionView.ItemsSource = _addressSuggestions;
        AddressEntry.TextChanged += OnAddressTextChanged;

        if (AddressLookupClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            AddressLookupClient.DefaultRequestHeaders.UserAgent.ParseAdd("ZesTour-Mobile/1.0");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!string.Equals(_authService.CurrentUser?.Role, "seller", StringComparison.OrdinalIgnoreCase))
        {
            await DisplayAlertAsync("Không có quyền", "Chỉ tài khoản Người bán mới có thể quản lý cửa hàng.", "OK");
            await Navigation.PopAsync();
            return;
        }

        await LoadMyStoresAsync();
    }

    protected override void OnDisappearing()
    {
        _addressLookupCts?.Cancel();
        base.OnDisappearing();
    }

    private async Task LoadMyStoresAsync()
    {
        StatusLabel.Text = string.Empty;

        var owners = GetOwnerLookupValues();
        if (owners.Count == 0)
        {
            StatusLabel.Text = "Không xác định được chủ cửa hàng hiện tại.";
            RegistrationsCollection.ItemsSource = null;
            ClearEditor();
            return;
        }

        _items = new List<ApiService.ManagementStoreRegistration>();
        foreach (var owner in owners)
        {
            var stores = await _apiService.GetMyStoreRegistrationsAsync(owner);
            foreach (var store in stores)
            {
                if (_items.Any(existing => existing.Id == store.Id))
                {
                    continue;
                }

                _items.Add(store);
            }
        }

        _items = _items.OrderByDescending(item => item.SubmittedAtUtc).ToList();
        RegistrationsCollection.ItemsSource = _items;

        if (_items.Count == 0)
        {
            StatusLabel.Text = "Bạn chưa có đăng ký cửa hàng nào.";
            ClearEditor();
            return;
        }

        _selected = _items[0];
        RegistrationsCollection.SelectedItem = _selected;
        FillEditor(_selected);
    }

    private void FillEditor(ApiService.ManagementStoreRegistration item)
    {
        StoreNameEntry.Text = item.StoreName;
        PhoneEntry.Text = item.Phone;
        SetAddressTextWithoutLookup(item.Address);
        CategoryEntry.Text = item.Category;
        DescriptionEditor.Text = item.Description;

        _primaryImageUrl = item.ImageUrl?.Trim();
        _selectedAddressGeo = item.Latitude.HasValue && item.Longitude.HasValue
            ? new AddressGeoPoint(item.Latitude.Value, item.Longitude.Value)
            : null;

        SetImageUrlRows(item.ImageUrls);

        if (_selectedAddressGeo is not null)
        {
            ShowAddressPreviewMap(_selectedAddressGeo.Value.Latitude, _selectedAddressGeo.Value.Longitude, item.Address);
        }
        else if (!string.IsNullOrWhiteSpace(item.Address))
        {
            _ = TryPreviewAddressAsync(item.Address);
        }
    }

    private void ClearEditor()
    {
        _selected = null;
        StoreNameEntry.Text = string.Empty;
        PhoneEntry.Text = string.Empty;
        SetAddressTextWithoutLookup(string.Empty);
        CategoryEntry.Text = string.Empty;
        DescriptionEditor.Text = string.Empty;
        _primaryImageUrl = null;
        _selectedAddressGeo = null;
        _addressSuggestions.Clear();
        AddressSuggestionsCollectionView.IsVisible = false;
        AddressMapPreviewContainer.IsVisible = false;
        SetImageUrlRows([]);
    }

    private void SetAddressTextWithoutLookup(string? value)
    {
        _isApplyingAddressSuggestion = true;
        AddressEntry.Text = value ?? string.Empty;
        _isApplyingAddressSuggestion = false;
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

        SetAddressTextWithoutLookup(selected.DisplayName);
        _selectedAddressGeo = new AddressGeoPoint(selected.Latitude, selected.Longitude);
        _addressSuggestions.Clear();
        AddressSuggestionsCollectionView.IsVisible = false;

        ShowAddressPreviewMap(selected.Latitude, selected.Longitude, selected.DisplayName);
    }

    private async Task TryPreviewAddressAsync(string? address)
    {
        var input = address?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            AddressMapPreviewContainer.IsVisible = false;
            return;
        }

        try
        {
            var geo = await ResolveAddressGeoAsync(input, CancellationToken.None);
            if (geo is null)
            {
                AddressMapPreviewContainer.IsVisible = false;
                return;
            }

            ShowAddressPreviewMap(geo.Value.Latitude, geo.Value.Longitude, input);
        }
        catch
        {
            AddressMapPreviewContainer.IsVisible = false;
        }
    }

    private static async Task<AddressGeoPoint?> ResolveAddressGeoAsync(string address, CancellationToken cancellationToken)
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

    private void SetImageUrlRows(IEnumerable<string>? imageUrls)
    {
        _previewItems.Clear();

        var normalized = imageUrls?
            .Select(url => url?.Trim())
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        if (normalized.Count == 0 && !string.IsNullOrWhiteSpace(_selected?.ImageUrl))
        {
            normalized.Add(_selected.ImageUrl.Trim());
        }

        if (normalized.Count > 0 &&
            (string.IsNullOrWhiteSpace(_primaryImageUrl) ||
             !normalized.Any(url => string.Equals(url, _primaryImageUrl, StringComparison.OrdinalIgnoreCase))))
        {
            _primaryImageUrl = normalized[0];
        }

        if (normalized.Count == 0)
        {
            StoreImagePreviewCollection.IsVisible = false;
            EnsurePrimaryImageExists();
            return;
        }

        foreach (var imageUrl in normalized)
        {
            if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri))
            {
                _previewItems.Add(new ImagePreviewItem(imageUrl, string.Equals(imageUrl, _primaryImageUrl, StringComparison.OrdinalIgnoreCase), ImageSource.FromUri(imageUri)));
            }
        }

        EnsurePrimaryImageExists();
        StoreImagePreviewCollection.IsVisible = _previewItems.Count > 0;
    }

    private async void OnAddImageUrlClicked(object? sender, EventArgs e)
    {
        StatusLabel.Text = string.Empty;

        try
        {
            var photos = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "Chọn ảnh cho cửa hàng",
                FileTypes = FilePickerFileType.Images
            });

            if (photos is null)
            {
                return;
            }

            var addedCount = 0;
            foreach (var photo in photos)
            {
                if (photo is null)
                {
                    continue;
                }

                await using var stream = await photo.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);

                var upload = await _apiService.UploadStoreImageAsync(ms.ToArray(), photo.FileName, photo.ContentType);
                if (!upload.Success || string.IsNullOrWhiteSpace(upload.ImageUrl))
                {
                    StatusLabel.TextColor = Color.FromArgb("#B91C1C");
                    StatusLabel.Text = upload.Message;
                    continue;
                }

                var imageUrl = upload.ImageUrl.Trim();
                if (_previewItems.Any(item => string.Equals(item.Url, imageUrl, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri))
                {
                    continue;
                }

                _previewItems.Add(new ImagePreviewItem(imageUrl, false, ImageSource.FromUri(imageUri)));
                addedCount++;
            }

            EnsurePrimaryImageExists();
            StoreImagePreviewCollection.IsVisible = _previewItems.Count > 0;

            if (addedCount > 0)
            {
                StatusLabel.TextColor = Color.FromArgb("#8E2F18");
                StatusLabel.Text = $"Đã thêm {addedCount} ảnh từ máy.";
            }
        }
        catch (Exception ex)
        {
            StatusLabel.TextColor = Color.FromArgb("#B91C1C");
            StatusLabel.Text = "Không thể chọn ảnh từ máy.";
            Console.WriteLine($"Lỗi chọn ảnh trong quản lý cửa hàng: {ex.Message}");
        }
    }

    private void OnRemoveImageUrlClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: ImagePreviewItem imageItem })
        {
            return;
        }

        var wasPrimary = string.Equals(imageItem.Url, _primaryImageUrl, StringComparison.OrdinalIgnoreCase);
        _previewItems.Remove(imageItem);

        if (wasPrimary)
        {
            _primaryImageUrl = null;
            EnsurePrimaryImageExists();
        }

        StoreImagePreviewCollection.IsVisible = _previewItems.Count > 0;
    }

    private void OnSetImageUrlPrimaryClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: ImagePreviewItem imageItem })
        {
            return;
        }

        _primaryImageUrl = imageItem.Url;
        EnsurePrimaryImageExists();
    }

    private List<string> GetImageUrlsFromEditor()
    {
        return _previewItems
            .Select(item => item.Url)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string? GetPrimaryImageUrlFromEditor()
    {
        if (_previewItems.Count == 0)
        {
            return null;
        }

        var normalizedPrimary = _primaryImageUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedPrimary))
        {
            var matched = _previewItems.Select(item => item.Url)
                .FirstOrDefault(url => string.Equals(url, normalizedPrimary, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(matched))
            {
                return matched;
            }
        }

        return _previewItems.Select(item => item.Url).FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        await LoadMyStoresAsync();
    }

    private void OnRegistrationSelected(object? sender, SelectionChangedEventArgs e)
    {
        var item = e.CurrentSelection?.FirstOrDefault() as ApiService.ManagementStoreRegistration;
        if (item is null)
        {
            return;
        }

        _selected = item;
        FillEditor(item);
    }

    private async void OnUpdateClicked(object? sender, EventArgs e)
    {
        StatusLabel.Text = string.Empty;

        if (_selected is null)
        {
            StatusLabel.Text = "Vui lòng chọn một đăng ký để cập nhật.";
            return;
        }

        var ownerName = _selected.OwnerName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(ownerName))
        {
            StatusLabel.Text = "Không xác định được chủ cửa hàng hiện tại.";
            return;
        }

        var storeName = StoreNameEntry.Text?.Trim() ?? string.Empty;
        var phone = PhoneEntry.Text?.Trim() ?? string.Empty;
        var address = AddressEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(storeName) || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(address))
        {
            StatusLabel.Text = "Tên cửa hàng, số điện thoại và địa chỉ là bắt buộc.";
            return;
        }

        var resolvedGeo = _selectedAddressGeo ?? await ResolveAddressGeoAsync(address, CancellationToken.None);
        if (resolvedGeo is null)
        {
            StatusLabel.Text = "Không xác định được vị trí địa chỉ. Vui lòng chọn gợi ý phù hợp.";
            return;
        }

        var radiusMeters = _selected.RadiusMeters is > 0 ? _selected.RadiusMeters : DefaultStoreRadiusMeters;

        _selected.StoreName = storeName;
        _selected.Phone = phone;
        _selected.Address = address;
        _selected.Latitude = resolvedGeo.Value.Latitude;
        _selected.Longitude = resolvedGeo.Value.Longitude;
        _selected.RadiusMeters = radiusMeters;
        _selected.Category = CategoryEntry.Text?.Trim() ?? string.Empty;
        _selected.Description = DescriptionEditor.Text?.Trim() ?? string.Empty;
        _selected.ImageUrls = GetImageUrlsFromEditor();
        _selected.ImageUrl = GetPrimaryImageUrlFromEditor();

        var result = await _apiService.UpdateMyStoreRegistrationAsync(_selected, ownerName);
        StatusLabel.TextColor = result.Success ? Color.FromArgb("#8E2F18") : Color.FromArgb("#B91C1C");
        StatusLabel.Text = result.Message;
        if (result.Success)
        {
            await LoadMyStoresAsync();
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        StatusLabel.Text = string.Empty;

        if (_selected is null)
        {
            StatusLabel.Text = "Vui lòng chọn một đăng ký để xóa.";
            return;
        }

        var confirm = await DisplayAlertAsync("Xác nhận", "Bạn có chắc muốn xóa đăng ký này?", "Xóa", "Hủy");
        if (!confirm)
        {
            return;
        }

        var ownerName = _selected.OwnerName?.Trim() ?? string.Empty;
        var result = await _apiService.DeleteMyStoreRegistrationAsync(_selected.Id, ownerName);
        StatusLabel.TextColor = result.Success ? Color.FromArgb("#8E2F18") : Color.FromArgb("#B91C1C");
        StatusLabel.Text = result.Message;

        if (result.Success)
        {
            await LoadMyStoresAsync();
        }
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        await Navigation.PopAsync();
    }

    private void EnsurePrimaryImageExists()
    {
        if (!string.IsNullOrWhiteSpace(_primaryImageUrl) &&
            _previewItems.Any(item => string.Equals(item.Url, _primaryImageUrl, StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var preview in _previewItems)
            {
                preview.IsPrimary = string.Equals(preview.Url, _primaryImageUrl, StringComparison.OrdinalIgnoreCase);
            }

            return;
        }

        _primaryImageUrl = _previewItems
            .Select(item => item.Url)
            .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));

        foreach (var preview in _previewItems)
        {
            preview.IsPrimary = string.Equals(preview.Url, _primaryImageUrl, StringComparison.OrdinalIgnoreCase);
        }
    }

    private List<string> GetOwnerLookupValues()
    {
        var values = new List<string>();

        var displayName = _authService.CurrentUser?.Name?.Trim();
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            values.Add(displayName);
        }

        var username = _authService.CurrentUser?.Username?.Trim();
        if (!string.IsNullOrWhiteSpace(username) && !values.Any(value => string.Equals(value, username, StringComparison.OrdinalIgnoreCase)))
        {
            values.Add(username);
        }

        return values;
    }

    private sealed class ImagePreviewItem : INotifyPropertyChanged
    {
        private bool _isPrimary;

        public ImagePreviewItem(string url, bool isPrimary, ImageSource source)
        {
            Url = url;
            _isPrimary = isPrimary;
            Source = source;
        }

        public string Url { get; }
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

        public ImageSource Source { get; }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private readonly record struct AddressGeoPoint(double Latitude, double Longitude);

    private sealed class AddressSuggestionItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

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