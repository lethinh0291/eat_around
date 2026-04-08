using MobileApp.Services;
using SharedLib.Models;
using System.Globalization;
using Microsoft.Maui.Devices.Sensors;
using System.Threading;
using System.Collections.Generic;

namespace ZesTour.Views;

public partial class MainPage : ContentPage
{
    private readonly DatabaseService _databaseService;
    private readonly ApiService _apiService;
    private readonly LocationService _locationService;
    private readonly Location _defaultLocation = new(10.7724, 106.6981);
    private readonly Location _vinhKhanhCenter = new(10.7589, 106.7072);
    private const double VinhKhanhRadiusMeters = 1200;
    private const double LeafletZoom = 16;
    private bool _autoPlayEnabled = true;
    private bool _batteryOptimized;
    private bool _dataLoaded;
    private bool _followUserRealtime;
    private POI? _currentPoi;
    private Location? _userLocation;
    private double? _selectedLat;
    private double? _selectedLng;
    private string _selectedName = string.Empty;
    private string _selectedDescription = string.Empty;
    private CancellationTokenSource? _trackingCts;

    public MainPage(DatabaseService databaseService, ApiService apiService, LocationService locationService)
    {
        _databaseService = databaseService;
        _apiService = apiService;
        _locationService = locationService;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_dataLoaded)
        {
            return;
        }

        await LoadPoiAsync();
        _dataLoaded = true;
    }

    protected override void OnDisappearing()
    {
        StopRealtimeTracking();
        base.OnDisappearing();
    }

    private async Task LoadPoiAsync()
    {
        _userLocation = await TryGetUserLocationAsync();

        var pois = await _databaseService.GetPOIsAsync();

        if (pois.Count == 0)
        {
            pois = await _apiService.GetPoisAsync();

            if (pois.Count > 0)
            {
                await _databaseService.SavePoisAsync(pois);
            }
        }

        _currentPoi = pois
            .OrderBy(p => _locationService.CalculateDistance(_vinhKhanhCenter.Latitude, _vinhKhanhCenter.Longitude, p.Latitude, p.Longitude))
            .FirstOrDefault();

        if (_currentPoi is null)
        {
            SetFallbackPoi();
            return;
        }

        BindPoi(_currentPoi);
        InitializeMap(_currentPoi);
    }

    private void SetFallbackPoi()
    {
        MapPoiLabel.Text = "Không có dữ liệu";
        PoiBadgeLabel.Text = "VĨNH KHÁNH";
        PoiNameLabel.Text = "Phố Ẩm Thực Vĩnh Khánh";
        PoiRatingLabel.Text = "0.0";
        PoiDescriptionLabel.Text = "Khu ẩm thực Vĩnh Khánh, Phường 10, Quận 4, TP.HCM.";
        NowPlayingLabel.Text = "ĐANG PHÁT: Phố Ẩm Thực Vĩnh Khánh";
        LoadLeafletMap(null);
    }

    private void BindPoi(POI poi)
    {
        var ratingText = poi.Priority >= 8 ? "4.8 ★★★★★" : poi.Priority >= 5 ? "4.5 ★★★★☆" : "4.2 ★★★★☆";
        var description = string.IsNullOrWhiteSpace(poi.Description)
            ? "Chưa có mô tả cho địa điểm này."
            : poi.Description;

        MapPoiLabel.Text = poi.Name;
        PoiBadgeLabel.Text = string.IsNullOrWhiteSpace(poi.LanguageCode) ? "POI" : poi.LanguageCode.ToUpperInvariant();
        PoiNameLabel.Text = poi.Name;
        PoiRatingLabel.Text = ratingText;
        PoiDescriptionLabel.Text = description;
        NowPlayingLabel.Text = $"ĐANG PHÁT: {poi.Name} (Nội dung tự động)";
    }

    private void InitializeMap(POI poi)
    {
        LoadLeafletMap(poi);
    }

    private void LoadLeafletMap(POI? poi, bool centerOnUser = false)
    {
        var center = centerOnUser && _userLocation is not null ? _userLocation : _vinhKhanhCenter;
        var poiLatitude = poi?.Latitude.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        var poiLongitude = poi?.Longitude.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        var poiName = EscapeJavaScript(poi?.Name ?? "Phố Ẩm Thực Vĩnh Khánh");
        var poiDescription = EscapeJavaScript(poi?.Description ?? "Phường 10, Quận 4, TP.HCM");
        var hasPoi = poi is not null && _locationService.CalculateDistance(center.Latitude, center.Longitude, poi.Latitude, poi.Longitude) <= VinhKhanhRadiusMeters;
        var hasUser = _userLocation is not null &&
                      (centerOnUser || _locationService.CalculateDistance(center.Latitude, center.Longitude, _userLocation.Latitude, _userLocation.Longitude) <= VinhKhanhRadiusMeters);
        var userLatitude = _userLocation?.Latitude.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        var userLongitude = _userLocation?.Longitude.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        const string routeProfile = "driving";
        const string routeColor = "#0B84F3";
        const string routeDashArray = "";

        var html = @"<!DOCTYPE html>
<html>
<head>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no' />
    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
    <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
    <style>
        html, body {
            height: 100%;
            width: 100%;
            margin: 0;
            padding: 0;
            background: #efe9dc;
            overflow: hidden;
        }
        #map {
            height: 100%;
            width: 100%;
            display: none;
        }
        #fallback {
            height: 100%;
            width: 100%;
            border: 0;
            display: none;
        }
        .street-pin {
            background: #e86f2d;
            color: #fff;
            padding: 6px 12px;
            border-radius: 999px;
            border: 2px solid rgba(255,255,255,0.9);
            box-shadow: 0 4px 14px rgba(0,0,0,0.18);
            font: 600 13px system-ui, -apple-system, Segoe UI, sans-serif;
            white-space: nowrap;
        }
        .street-center {
            width: 14px;
            height: 14px;
            border-radius: 50%;
            background: #4b8de8;
            border: 3px solid rgba(255,255,255,0.95);
            box-shadow: 0 0 0 8px rgba(75,141,232,0.18);
        }
        .user-dot {
            width: 16px;
            height: 16px;
            border-radius: 50%;
            background: #2d74da;
            border: 3px solid #ffffff;
            box-shadow: 0 0 0 10px rgba(45,116,218,0.2);
        }
    </style>
</head>
<body>
    <div id='map'></div>
    <iframe id='fallback' src='https://www.openstreetmap.org/export/embed.html?bbox=106.6992%2C10.7530%2C106.7152%2C10.7648&amp;layer=mapnik&amp;marker=10.7589%2C106.7072'></iframe>
    <script>
        const streetCenter = [__CENTER_LAT__, __CENTER_LNG__];
        const bounds = L.latLngBounds(
            [streetCenter[0] - 0.006, streetCenter[1] - 0.008],
            [streetCenter[0] + 0.006, streetCenter[1] + 0.008]
        );

        if (typeof L === 'undefined') {
            document.getElementById('fallback').style.display = 'block';
        } else {
            document.getElementById('map').style.display = 'block';
            const map = L.map('map', {
                zoomControl: true,
                attributionControl: false,
                touchZoom: true,
                doubleClickZoom: true,
                scrollWheelZoom: true,
                dragging: true
            });

            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                maxZoom: 19,
                minZoom: 15,
                bounds: bounds,
                noWrap: true
            }).addTo(map);

            map.setView(streetCenter, __LEAFLET_ZOOM__);
            map.setMaxBounds(bounds.pad(0.15));
            map.on('drag', () => map.panInsideBounds(bounds, { animate: false }));

            const userPos = __USER_POSITION__;
            let destinationMarker = null;
            let routeLine = null;

            function clearRoute() {
                if (routeLine) {
                    map.removeLayer(routeLine);
                    routeLine = null;
                }
                if (destinationMarker) {
                    map.removeLayer(destinationMarker);
                    destinationMarker = null;
                }
            }

            function notifySelection(name, desc, lat, lng) {
                const url = 'app://selected?name=' + encodeURIComponent(name)
                    + '&desc=' + encodeURIComponent(desc)
                    + '&lat=' + encodeURIComponent(lat)
                    + '&lng=' + encodeURIComponent(lng);
                window.location.href = url;
            }

            async function drawRouteTo(destLat, destLng, label, detail) {
                clearRoute();

                notifySelection(label, detail, destLat, destLng);

                destinationMarker = L.marker([destLat, destLng]).addTo(map);
                destinationMarker.bindPopup('<b>' + label + '</b>').openPopup();

                try {
                    const url = `https://router.project-osrm.org/route/v1/__ROUTE_PROFILE__/${userPos[1]},${userPos[0]};${destLng},${destLat}?overview=full&geometries=geojson`;
                    const response = await fetch(url);
                    const data = await response.json();

                    if (data && data.code === 'Ok' && data.routes && data.routes.length > 0) {
                        const coords = data.routes[0].geometry.coordinates.map(c => [c[1], c[0]]);
                        routeLine = L.polyline(coords, { color: '__ROUTE_COLOR__', weight: 5, opacity: 0.9, dashArray: '__ROUTE_DASH__' }).addTo(map);
                    } else {
                        routeLine = L.polyline([userPos, [destLat, destLng]], { color: '__ROUTE_COLOR__', weight: 4, dashArray: '__ROUTE_DASH__' }).addTo(map);
                    }
                } catch {
                    routeLine = L.polyline([userPos, [destLat, destLng]], { color: '__ROUTE_COLOR__', weight: 4, dashArray: '__ROUTE_DASH__' }).addTo(map);
                }

                if (routeLine) {
                    map.fitBounds(routeLine.getBounds(), { padding: [28, 28], maxZoom: 18 });
                }
            }

            async function resolvePlaceInfo(lat, lng) {
                const fallback = {
                    label: 'Điểm đã chọn',
                    detail: `Tọa độ: ${lat.toFixed(6)}, ${lng.toFixed(6)}`
                };

                function buildLabelAndDetail(rawName, rawDetail, isWorship = false) {
                    let label = (rawName || '').trim();
                    if (!label) {
                        return fallback;
                    }

                    if (isWorship && !label.toLowerCase().includes('nhà thờ')) {
                        label = `Nhà thờ ${label}`;
                    }

                    return {
                        label,
                        detail: (rawDetail || fallback.detail).trim()
                    };
                }

                try {
                    const url = `https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat=${lat}&lon=${lng}&zoom=18&addressdetails=1`;
                    const response = await fetch(url, {
                        headers: {
                            'Accept': 'application/json'
                        }
                    });

                    if (!response.ok) {
                        return fallback;
                    }

                    const data = await response.json();
                    const address = data.address || {};
                    const isWorship = data.type === 'place_of_worship' || address.amenity === 'place_of_worship';

                    const labelCandidates = [
                        data.name,
                        address.attraction,
                        address.building,
                        address.tourism,
                        address.shop,
                        address.office,
                        address.amenity,
                        address.road,
                        data.display_name ? data.display_name.split(',')[0] : null
                    ];

                    const label = labelCandidates.find(v => typeof v === 'string' && v.trim().length > 0);
                    if (label) {
                        return buildLabelAndDetail(label, data.display_name, isWorship);
                    }
                } catch {
                    // Try another provider if Nominatim fails.
                }

                try {
                    const photonUrl = `https://photon.komoot.io/reverse?lat=${lat}&lon=${lng}`;
                    const photonRes = await fetch(photonUrl, {
                        headers: {
                            'Accept': 'application/json'
                        }
                    });

                    if (photonRes.ok) {
                        const photon = await photonRes.json();
                        const feature = photon.features && photon.features.length > 0 ? photon.features[0] : null;
                        const p = feature ? (feature.properties || {}) : {};
                        const pLabel = p.name || p.street || p.housenumber || p.city || p.county;
                        const pDetail = [p.street, p.district, p.city, p.state, p.country]
                            .filter(Boolean)
                            .join(', ');

                        if (pLabel) {
                            return buildLabelAndDetail(pLabel, pDetail);
                        }
                    }
                } catch {
                    // Ignore and use fallback.
                }

                return fallback;
            }

            const centerIcon = L.divIcon({
                className: '',
                html: '<div class=""street-center""></div>',
                iconSize: [14, 14],
                iconAnchor: [7, 7]
            });
            L.marker(streetCenter, { icon: centerIcon }).addTo(map);

            const streetIcon = L.divIcon({
                className: '',
                html: '<div class=""street-pin"">Phố Ẩm Thực Vĩnh Khánh</div>',
                iconSize: [160, 34],
                iconAnchor: [80, 34]
            });
            const streetMarker = L.marker(streetCenter, { icon: streetIcon }).addTo(map);
            streetMarker.on('click', () => drawRouteTo(streetCenter[0], streetCenter[1], 'Phố Ẩm Thực Vĩnh Khánh', 'Phường 10, Quận 4, TP.HCM'));

            __USER_BLOCK__

            __POI_BLOCK__

            __SELECTED_ROUTE_BLOCK__

            map.on('click', async (e) => {
                const info = await resolvePlaceInfo(e.latlng.lat, e.latlng.lng);
                drawRouteTo(
                    e.latlng.lat,
                    e.latlng.lng,
                    info.label,
                    info.detail
                );
            });
        }
    </script>
</body>
</html>";

        html = html
            .Replace("__CENTER_LAT__", center.Latitude.ToString(CultureInfo.InvariantCulture))
            .Replace("__CENTER_LNG__", center.Longitude.ToString(CultureInfo.InvariantCulture))
            .Replace("__LEAFLET_ZOOM__", LeafletZoom.ToString(CultureInfo.InvariantCulture))
            .Replace("__ROUTE_PROFILE__", routeProfile)
            .Replace("__ROUTE_COLOR__", routeColor)
            .Replace("__ROUTE_DASH__", routeDashArray)
            .Replace("__USER_POSITION__", hasUser ? $"[{userLatitude}, {userLongitude}]" : "streetCenter")
            .Replace("__USER_BLOCK__", hasUser
                ? $"const userIcon = L.divIcon({{ className: '', html: '<div class=\\\"user-dot\\\"></div>', iconSize: [16,16], iconAnchor:[8,8] }}); L.marker([{userLatitude}, {userLongitude}], {{ icon: userIcon }}).addTo(map).bindPopup('Bạn đang ở đây');"
                : string.Empty)
            .Replace("__POI_BLOCK__", hasPoi
                ? $"const poiMarker = L.marker([{poiLatitude}, {poiLongitude}]).addTo(map).bindPopup('<b>{poiName}</b><br/>{poiDescription}'); poiMarker.on('click', () => drawRouteTo({poiLatitude}, {poiLongitude}, '{poiName}', '{poiDescription}'));"
                : string.Empty)
            .Replace("__SELECTED_ROUTE_BLOCK__", _selectedLat.HasValue && _selectedLng.HasValue
                ? $"drawRouteTo({_selectedLat.Value.ToString(CultureInfo.InvariantCulture)}, {_selectedLng.Value.ToString(CultureInfo.InvariantCulture)}, '{EscapeJavaScript(_selectedName)}', '{EscapeJavaScript(_selectedDescription)}');"
                : string.Empty);

        LeafletMapView.Source = new HtmlWebViewSource
        {
            Html = html
        };
    }

    private static string EscapeJavaScript(string value)
    {
        return value
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\r", string.Empty)
                .Replace("\n", " ");
    }

    private static async Task<Location?> TryGetUserLocationAsync()
    {
        try
        {
            var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(8));
            var current = await Geolocation.Default.GetLocationAsync(request);

            if (current is not null)
            {
                return current;
            }

            return await Geolocation.Default.GetLastKnownLocationAsync();
        }
        catch
        {
            return null;
        }
    }

    private void StartRealtimeTracking()
    {
        StopRealtimeTracking();

        _trackingCts = new CancellationTokenSource();
        var token = _trackingCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                var latest = await TryGetUserLocationAsync();
                if (latest is not null)
                {
                    _userLocation = latest;
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (_followUserRealtime)
                        {
                            LoadLeafletMap(_currentPoi, centerOnUser: true);
                        }
                    });
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }, token);
    }

    private void StopRealtimeTracking()
    {
        if (_trackingCts is null)
        {
            return;
        }

        _trackingCts.Cancel();
        _trackingCts.Dispose();
        _trackingCts = null;
    }

    private async void OnMenuTapped(object? sender, TappedEventArgs e)
    {
        await DisplayAlertAsync("Menu", "Mở menu tính năng.", "OK");
    }

    private void OnMapNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Url) || !e.Url.StartsWith("app://selected", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        e.Cancel = true;

        var uri = new Uri(e.Url);
        var query = ParseQuery(uri.Query);

        var name = query.TryGetValue("name", out var selectedName)
            ? selectedName
            : "Điểm đã chọn";

        var description = query.TryGetValue("desc", out var selectedDesc)
            ? selectedDesc
            : "Không có mô tả";

        var lat = query.TryGetValue("lat", out var selectedLat) ? selectedLat : string.Empty;
        var lng = query.TryGetValue("lng", out var selectedLng) ? selectedLng : string.Empty;

        if (double.TryParse(lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLat) &&
            double.TryParse(lng, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLng))
        {
            _selectedLat = parsedLat;
            _selectedLng = parsedLng;
            _selectedName = name;
            _selectedDescription = description;
        }

        NearbyIntroLabel.Text = "Địa điểm bạn chọn trên bản đồ:";
        PoiBadgeLabel.Text = "ĐÃ CHỌN";
        PoiNameLabel.Text = name;
        PoiDescriptionLabel.Text = description;
        PoiRatingLabel.Text = string.IsNullOrWhiteSpace(lat) || string.IsNullOrWhiteSpace(lng)
            ? "-"
            : $"{lat}, {lng}";
        NowPlayingLabel.Text = $"ĐANG PHÁT: {name}";
        MapPoiLabel.Text = name;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
        {
            return result;
        }

        var trimmed = query.TrimStart('?');
        foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            var key = Uri.UnescapeDataString(pair[0]);
            var value = pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : string.Empty;
            result[key] = value;
        }

        return result;
    }

    private async void OnNotificationTapped(object? sender, TappedEventArgs e)
    {
        await DisplayAlertAsync("Thông báo", "Bạn chưa có thông báo mới.", "OK");
    }

    private async void OnInfoTapped(object? sender, TappedEventArgs e)
    {
        await DisplayAlertAsync("Thông tin", "Khu vực này đang có 12 điểm ăn uống nổi bật.", "OK");
    }

    private async void OnRecenterTapped(object? sender, TappedEventArgs e)
    {
        _userLocation = await TryGetUserLocationAsync();

        if (_userLocation is null)
        {
            await DisplayAlertAsync("Vị trí", "Không lấy được vị trí hiện tại của bạn.", "OK");
            return;
        }

        _followUserRealtime = true;
        StartRealtimeTracking();
        LoadLeafletMap(_currentPoi, centerOnUser: true);
        await DisplayAlertAsync("Vị trí", "Đã bật theo dõi realtime vị trí của bạn.", "OK");
    }

    private async void OnNearbyCardTapped(object? sender, TappedEventArgs e)
    {
        if (_currentPoi is null)
        {
            await DisplayAlertAsync("Địa điểm", "Chưa có dữ liệu từ database.", "Đóng");
            return;
        }

        var status = _autoPlayEnabled ? "Bật" : "Tắt";
        var description = string.IsNullOrWhiteSpace(_currentPoi.Description)
            ? "Chưa có mô tả."
            : _currentPoi.Description;

        await DisplayAlertAsync(_currentPoi.Name, $"{description}\nTự động phát hiện: {status}", "Đóng");
    }

    private async void OnAutoPlayToggled(object? sender, ToggledEventArgs e)
    {
        _autoPlayEnabled = e.Value;
        var text = e.Value ? "Đã bật Tự Động Phát." : "Đã tắt Tự Động Phát.";
        await DisplayAlertAsync("Cập nhật", text, "OK");
    }

    private async void OnBatteryOptimizedToggled(object? sender, ToggledEventArgs e)
    {
        _batteryOptimized = e.Value;
        var text = _batteryOptimized ? "Đang tối ưu pin." : "Đã tắt tối ưu pin.";
        await DisplayAlertAsync("Cập nhật", text, "OK");
    }
}
