using MobileApp.Services;
using MobileApp.Resources.Localization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ZesTour.Views;

public partial class StoreManagementPage : ContentPage
{
    private const double DefaultStoreRadiusMeters = 140;
    private static readonly HttpClient AddressLookupClient = new();

    private readonly ApiService _apiService;
    private readonly AuthService _authService;
    private readonly ObservableCollection<ImagePreviewItem> _previewItems = new();
    private readonly ObservableCollection<AddressSuggestionItem> _addressSuggestions = new();
    public string MyStoreBadgeText => AppText.Get("StoreManagement_MyStoreBadge");

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
        ApplyLocalizedText();

        StoreImagePreviewCollection.ItemsSource = _previewItems;
        AddressSuggestionsCollectionView.ItemsSource = _addressSuggestions;
        AddressEntry.TextChanged += OnAddressTextChanged;

        if (AddressLookupClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            AddressLookupClient.DefaultRequestHeaders.UserAgent.ParseAdd("ZesTour-Mobile/1.0");
        }
    }

    private void ApplyLocalizedText()
    {
        ManagementTitleLabel.Text = AppText.Get("StoreManagement_Title");
        ManagementSubtitleLabel.Text = AppText.Get("StoreManagement_Subtitle");
        EditRegistrationLabel.Text = AppText.Get("StoreManagement_EditRegistration");
        EditRegistrationTitleLabel.Text = AppText.Get("StoreManagement_EditRegistrationTitle");
        EditRegistrationHintLabel.Text = AppText.Get("StoreManagement_EditRegistrationHint");
        RefreshButton.Text = AppText.Get("StoreManagement_Refresh");
        RegistrationsTitleLabel.Text = AppText.Get("StoreManagement_RegistrationListTitle");
        RegistrationsSubtitleLabel.Text = AppText.Get("StoreManagement_RegistrationListSubtitle");
        SelectOneBadgeLabel.Text = AppText.Get("StoreManagement_SelectOne");
        StoreInfoTitleLabel.Text = AppText.Get("StoreManagement_StoreInfoTitle");
        StoreInfoSubtitleLabel.Text = AppText.Get("StoreManagement_StoreInfoSubtitle");
        StoreNameEntry.Placeholder = AppText.Get("StoreManagement_StoreName");
        PhoneEntry.Placeholder = AppText.Get("StoreManagement_Phone");
        CategoryEntry.Placeholder = AppText.Get("StoreManagement_Category");
        AddressLabel.Text = AppText.Get("StoreManagement_Address");
        AddressEntry.Placeholder = AppText.Get("StoreManagement_AddressPlaceholder");
        AddressMapPreviewLabel.Text = AppText.Get("StoreManagement_AddressPreview");
        AddressPreviewHintLabel.Text = AppText.Get("StoreManagement_AddressPreviewHint");
        PreviewBadgeLabel.Text = AppText.Get("StoreManagement_Preview");
        DescriptionEditor.Placeholder = AppText.Get("StoreManagement_Description");
        AddImageButton.Text = AppText.Get("StoreManagement_AddImage");
        AddImageHintLabel.Text = AppText.Get("StoreManagement_AddImageHint");
        AlbumPreviewTitleLabel.Text = AppText.Get("StoreManagement_AlbumPreview");
        AlbumPreviewHintLabel.Text = AppText.Get("StoreManagement_AlbumPreviewHint");
        AlbumBadgeLabel.Text = AppText.Get("StoreManagement_AlbumBadge");
        UpdateButton.Text = AppText.Get("StoreManagement_Update");
        DeleteButton.Text = AppText.Get("StoreManagement_Delete");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!string.Equals(_authService.CurrentUser?.Role, "seller", StringComparison.OrdinalIgnoreCase))
        {
            await DisplayAlertAsync(
                AppText.Get("Common_NoPermissionTitle"),
                AppText.Get("StoreManagement_SellerOnly"),
                AppText.Get("Common_Ok"));
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
            StatusLabel.Text = AppText.Get("StoreManagement_CannotResolveOwner");
            RegistrationsCollection.ItemsSource = null;
            RegistrationsCollection.IsVisible = false;
            EmptyStateCard.IsVisible = true;
            ClearEditor();
            await HideEditorModalAsync(false);
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

        var livePois = await _apiService.GetPoisAsync();
        foreach (var poi in livePois)
        {
            var poiOwner = ExtractOwnerNameFromPoiDescription(poi.Description);
            if (string.IsNullOrWhiteSpace(poiOwner) ||
                !owners.Any(owner => string.Equals(owner, poiOwner, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var mapped = MapPoiToManagementStoreRegistration(poi, poiOwner);
            if (_items.Any(existing => existing.Id == mapped.Id))
            {
                continue;
            }

            _items.Add(mapped);
        }

        _items = _items.OrderByDescending(item => item.SubmittedAtUtc).ToList();
        RegistrationsCollection.ItemsSource = _items;

        if (_items.Count == 0)
        {
            StatusLabel.Text = AppText.Get("StoreManagement_NoRegistrations");
            EmptyStateTitleLabel.Text = AppText.Get("StoreManagement_NoRegistrations");
            EmptyStateSubtitleLabel.Text = "Hãy gửi đăng ký quán mới hoặc tải lại sau khi quán được duyệt.";
            RegistrationsCollection.IsVisible = false;
            EmptyStateCard.IsVisible = true;
            ClearEditor();
            await HideEditorModalAsync(false);
            return;
        }

        RegistrationsCollection.IsVisible = true;
        EmptyStateCard.IsVisible = false;
        _selected = null;
        ClearEditor();
        await HideEditorModalAsync(false);
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
        AddressMapPreviewLabel.Text = AppText.Format("StoreManagement_AddressPositionFormat", title);

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
                PickerTitle = AppText.Get("StoreManagement_PickImages"),
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
                StatusLabel.Text = AppText.Format("StoreManagement_AddedImagesFormat", addedCount);
            }
        }
        catch (Exception ex)
        {
            StatusLabel.TextColor = Color.FromArgb("#B91C1C");
            StatusLabel.Text = AppText.Get("StoreManagement_PickImagesFailed");
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

    private async void OnEditStoreClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not ApiService.ManagementStoreRegistration item)
        {
            return;
        }

        _selected = item;
        FillEditor(item);
        await ShowEditorModalAsync();
        await Task.CompletedTask;
    }

    private async void OnCloseEditorTapped(object? sender, TappedEventArgs e)
    {
        await HideEditorModalAsync();
    }

    private async void OnEditorBackdropTapped(object? sender, TappedEventArgs e)
    {
        await HideEditorModalAsync();
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
            StatusLabel.Text = AppText.Get("StoreManagement_SelectToUpdate");
            return;
        }

        var ownerName = _selected.OwnerName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(ownerName))
        {
            StatusLabel.Text = AppText.Get("StoreManagement_CannotResolveOwner");
            return;
        }

        var storeName = StoreNameEntry.Text?.Trim() ?? string.Empty;
        var phone = PhoneEntry.Text?.Trim() ?? string.Empty;
        var address = AddressEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(storeName) || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(address))
        {
            StatusLabel.Text = AppText.Get("StoreManagement_RequiredFields");
            return;
        }

        var resolvedGeo = _selectedAddressGeo ?? await ResolveAddressGeoAsync(address, CancellationToken.None);
        if (resolvedGeo is null)
        {
            StatusLabel.Text = AppText.Get("StoreManagement_AddressNotResolved");
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

        if (_selected.IsLivePoi)
        {
            var livePoi = await ResolveLivePoiForUpdateAsync(_selected);
            if (livePoi is null)
            {
                StatusLabel.TextColor = Color.FromArgb("#B91C1C");
                StatusLabel.Text = "Không tìm thấy quán đã duyệt để cập nhật.";
                return;
            }

            var livePoiResult = await _apiService.UpdatePoiAsync(livePoi);
            StatusLabel.TextColor = livePoiResult.Success ? Color.FromArgb("#8E2F18") : Color.FromArgb("#B91C1C");
            StatusLabel.Text = livePoiResult.Message;

            if (livePoiResult.Success)
            {
                await DisplayAlertAsync(
                    "Thông báo",
                    "Cập nhật thông tin quán thành công.",
                    AppText.Get("Common_Ok"));
                await HideEditorModalAsync();
                await LoadMyStoresAsync();
            }

            return;
        }

        var result = await _apiService.UpdateMyStoreRegistrationAsync(_selected, ownerName);
        StatusLabel.TextColor = result.Success ? Color.FromArgb("#8E2F18") : Color.FromArgb("#B91C1C");
        StatusLabel.Text = result.Message;
        if (result.Success)
        {
            await DisplayAlertAsync(
                "Thông báo",
                "Cập nhật thông tin quán thành công.",
                AppText.Get("Common_Ok"));
            await HideEditorModalAsync();
            await LoadMyStoresAsync();
        }
    }

    private async Task<SharedLib.Models.POI?> ResolveLivePoiForUpdateAsync(ApiService.ManagementStoreRegistration registration)
    {
        var pois = await _apiService.GetPoisAsync();
        var currentPoi = pois.FirstOrDefault(poi => poi.Id == registration.Id);
        if (currentPoi is null)
        {
            return null;
        }

        currentPoi.Name = registration.StoreName;
        currentPoi.ImageUrl = string.IsNullOrWhiteSpace(registration.ImageUrl) ? null : registration.ImageUrl.Trim();
        currentPoi.Latitude = registration.Latitude ?? currentPoi.Latitude;
        currentPoi.Longitude = registration.Longitude ?? currentPoi.Longitude;
        currentPoi.Radius = registration.RadiusMeters is > 0 ? registration.RadiusMeters.Value : currentPoi.Radius;
        currentPoi.Description = BuildPoiDescriptionForUpdate(registration);

        return currentPoi;
    }

    private static string BuildPoiDescriptionForUpdate(ApiService.ManagementStoreRegistration registration)
    {
        var owner = string.IsNullOrWhiteSpace(registration.OwnerName) ? "Không rõ" : registration.OwnerName.Trim();
        var phone = string.IsNullOrWhiteSpace(registration.Phone) ? "--" : registration.Phone.Trim();
        var address = string.IsNullOrWhiteSpace(registration.Address) ? "Chưa có thông tin" : registration.Address.Trim();
        var category = string.IsNullOrWhiteSpace(registration.Category) ? "Ẩm thực" : registration.Category.Trim();
        var narration = string.IsNullOrWhiteSpace(registration.Description) ? "Chưa có mô tả" : registration.Description.Trim();

        return $"Liên hệ: {owner} - {phone} | Địa chỉ: {address} | Loại hình: {category} | Mô tả: {narration}";
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        StatusLabel.Text = string.Empty;

        if (_selected is null)
        {
            StatusLabel.Text = AppText.Get("StoreManagement_SelectToDelete");
            return;
        }

        if (_selected.IsLivePoi)
        {
            StatusLabel.Text = "Quán đã được duyệt nên không thể xóa tại màn hình này.";
            return;
        }

        var confirm = await DisplayAlertAsync(
            AppText.Get("StoreManagement_DeleteConfirmTitle"),
            AppText.Get("StoreManagement_DeleteConfirmMessage"),
            AppText.Get("StoreManagement_Delete"),
            AppText.Get("StoreManagement_Cancel"));
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
            await HideEditorModalAsync();
            await LoadMyStoresAsync();
        }
    }

    private async Task ShowEditorModalAsync()
    {
        if (StoreEditorOverlay.IsVisible)
        {
            return;
        }

        StoreEditorOverlay.Opacity = 0;
        StoreEditorPanel.Opacity = 0;
        StoreEditorPanel.TranslationY = 20;
        StoreEditorOverlay.IsVisible = true;

        await Task.WhenAll(
            StoreEditorOverlay.FadeToAsync(1, 160, Easing.CubicOut),
            StoreEditorPanel.FadeToAsync(1, 170, Easing.CubicOut),
            StoreEditorPanel.TranslateToAsync(0, 0, 180, Easing.CubicOut)
        );
    }

    private async Task HideEditorModalAsync(bool animated = true)
    {
        if (!StoreEditorOverlay.IsVisible)
        {
            return;
        }

        if (animated)
        {
            await Task.WhenAll(
                StoreEditorOverlay.FadeToAsync(0, 120, Easing.CubicIn),
                StoreEditorPanel.FadeToAsync(0, 120, Easing.CubicIn),
                StoreEditorPanel.TranslateToAsync(0, 16, 120, Easing.CubicIn)
            );
        }
        else
        {
            StoreEditorOverlay.Opacity = 0;
            StoreEditorPanel.Opacity = 0;
            StoreEditorPanel.TranslationY = 20;
        }

        StoreEditorOverlay.IsVisible = false;
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

    private static ApiService.ManagementStoreRegistration MapPoiToManagementStoreRegistration(SharedLib.Models.POI poi, string ownerName)
    {
        var contactPhone = ExtractContactPhoneFromPoiDescription(poi.Description);
        var mappedAddress = ExtractAddressFromPoiDescription(poi.Description);
        var mappedCategory = ExtractCategoryFromPoiDescription(poi.Description);
        var mappedDescription = ExtractNarrationFromPoiDescription(poi.Description);

        return new ApiService.ManagementStoreRegistration
        {
            Id = poi.Id,
            StoreName = poi.Name,
            OwnerName = ownerName,
            ImageUrl = poi.ImageUrl,
            ImageUrls = string.IsNullOrWhiteSpace(poi.ImageUrl) ? [] : new List<string> { poi.ImageUrl.Trim() },
            Phone = contactPhone,
            Address = mappedAddress,
            Latitude = poi.Latitude,
            Longitude = poi.Longitude,
            RadiusMeters = poi.Radius,
            Category = mappedCategory,
            Description = mappedDescription,
            SubmittedAtUtc = DateTime.MinValue,
            IsLivePoi = true
        };
    }

    private static string ExtractOwnerNameFromPoiDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        var match = Regex.Match(description, @"Liên hệ:\s*(.*?)\s*-", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static string ExtractContactPhoneFromPoiDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        var match = Regex.Match(description, @"Liên hệ:\s*.*?\s*-\s*([^|]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static string ExtractAddressFromPoiDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        var match = Regex.Match(description, @"Địa chỉ:\s*([^|]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static string ExtractCategoryFromPoiDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        var match = Regex.Match(description, @"Loại hình:\s*([^|]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static string ExtractNarrationFromPoiDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        var match = Regex.Match(description, @"Mô tả:\s*([^|]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : description.Trim();
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
        public string PrimaryBadgeText => AppText.Get("StoreManagement_PrimaryImage");

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