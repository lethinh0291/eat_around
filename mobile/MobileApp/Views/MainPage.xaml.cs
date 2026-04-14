using MobileApp.Services;
using SharedLib.Models;
using System.Globalization;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;
using System.Threading;
using System.Collections.Generic;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Json;

namespace ZesTour.Views;

public partial class MainPage : ContentPage
{
    private readonly DatabaseService _databaseService;
    private readonly ApiService _apiService;
    private readonly LocationService _locationService;
    private readonly AppNavigator _navigator;
    private readonly Location _defaultLocation = new(10.7724, 106.6981);
    private readonly Location _vinhKhanhCenter = new(10.7589, 106.7072);
    private static readonly HttpClient StoreGeoLookupClient = new();
    private const double VinhKhanhRadiusMeters = 1200;
    private const double VinhKhanhMinLat = 10.7530;
    private const double VinhKhanhMaxLat = 10.7648;
    private const double VinhKhanhMinLng = 106.6992;
    private const double VinhKhanhMaxLng = 106.7152;
    private const double LeafletZoom = 16;
    private const string SettingsKey = "zes_settings_v1";
    private const string TripsHistoryKey = "zes_trip_history_v1";
    private const double DefaultStoreNarrationRadiusMeters = 140;
    private bool _autoPlayEnabled = true;
    private bool _batteryOptimized;
    private bool _storeNarrationEnabled = true;
    private bool _isSidebarOpen;
    private bool _sidebarAnimating;
    private bool _dataLoaded;
    private CancellationTokenSource? _loadingCts;
    private DateTime _lastRecenterAt = DateTime.MinValue;
    private CancellationTokenSource? _trackingCts;
    private Task? _trackingTask;
    private List<POI> _allPois = new();
    private List<StoreNarrationPoint> _storeNarrationPoints = new();
    private POI? _currentPoi;
    private Location? _userLocation;
    private double? _selectedLat;
    private double? _selectedLng;
    private string _selectedName = string.Empty;
    private string _selectedDescription = string.Empty;
    private bool _hasExplicitMapSelection;
    private static readonly TimeSpan NormalTrackingInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan BatteryTrackingInterval = TimeSpan.FromSeconds(18);

    public MainPage(DatabaseService databaseService, ApiService apiService, LocationService locationService, AppNavigator navigator)
    {
        _databaseService = databaseService;
        _apiService = apiService;
        _locationService = locationService;
        _navigator = navigator;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_dataLoaded)
        {
            return;
        }

        _loadingCts?.Cancel();
        _loadingCts = new CancellationTokenSource();

        LoadFeatureSettings();

        // Render a lightweight default map immediately to avoid a blank first frame.
        InitializeMap(null);

        try
        {
            await LoadPoiAsync(_loadingCts.Token);
            _dataLoaded = true;
        }
        catch (OperationCanceledException)
        {
            // Ignore when the page disappears while loading.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Data loading error: {ex}");
            await DisplayAlertAsync("Lỗi", $"Lỗi tải dữ liệu: {ex.Message}", "OK");
        }
    }

    protected override void OnDisappearing()
    {
        _loadingCts?.Cancel();
        StopRealtimeTracking();
        base.OnDisappearing();
    }

    private async Task LoadPoiAsync(CancellationToken cancellationToken)
    {
        var locationTask = TryGetUserLocationAsync(cancellationToken);

        var pois = await _databaseService.GetPOIsAsync();
        cancellationToken.ThrowIfCancellationRequested();

        if (pois.Count == 0)
        {
            pois = await _apiService.GetPoisAsync();
            cancellationToken.ThrowIfCancellationRequested();

            if (pois.Count > 0)
            {
                await _databaseService.SavePoisAsync(pois);
            }
        }

        _allPois = pois.Where(IsInsideVinhKhanhBounds).ToList();
        await LoadStoreNarrationPointsAsync(cancellationToken);

        _currentPoi = FindNearestPoi(_allPois);

        if (_currentPoi is null)
        {
            PoiBadgeLabel.Text = "ĐỊA ĐIỂM";
            LocationTitleLabel.Text = "Địa điểm trên bản đồ";
            NowPlayingLabel.Text = "Chạm vào bản đồ để chọn địa điểm";
            AddressLabel.Text = "Số nhà, đường: Chưa có thông tin";
            InitializeMap(null);
            StartRealtimeTracking();
            _ = UpdateLocationAsync(locationTask, cancellationToken);
            return;
        }

        BindPoi(_currentPoi);
        InitializeMap(_currentPoi);
        StartRealtimeTracking();

        // Continue location refinement in background so first screen paints faster.
        _ = UpdateLocationAsync(locationTask, cancellationToken);
    }

    private async Task UpdateLocationAsync(Task<Location?> locationTask, CancellationToken cancellationToken)
    {
        try
        {
            _userLocation = await locationTask;
            cancellationToken.ThrowIfCancellationRequested();

            if (_userLocation is null)
            {
                return;
            }

            var nearestPoi = FindNearestPoi(_allPois);
            if (nearestPoi is not null)
            {
                _currentPoi = nearestPoi;
                MainThread.BeginInvokeOnMainThread(() => BindPoi(nearestPoi));
            }

            await MainThread.InvokeOnMainThreadAsync(() => RecenterMapAsync(_userLocation));
        }
        catch
        {
            // Ignore delayed geolocation failures to keep first render smooth.
        }
    }

    private POI? FindNearestPoi(List<POI> pois)
    {
        if (pois.Count == 0)
        {
            return null;
        }

        var reference = _userLocation ?? _vinhKhanhCenter;
        POI? nearest = null;
        var minDistance = double.MaxValue;

        foreach (var poi in pois)
        {
            var distance = _locationService.CalculateDistance(
                reference.Latitude,
                reference.Longitude,
                poi.Latitude,
                poi.Longitude);

            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = poi;
            }
        }

        return nearest;
    }

    private StoreNarrationPoint? FindNearestStoreInRange(Location userLocation)
    {
        StoreNarrationPoint? nearest = null;
        var minDistance = double.MaxValue;

        foreach (var store in _storeNarrationPoints)
        {
            var distance = _locationService.CalculateDistance(
                userLocation.Latitude,
                userLocation.Longitude,
                store.Latitude,
                store.Longitude);

            if (distance > store.RadiusMeters || distance >= minDistance)
            {
                continue;
            }

            minDistance = distance;
            nearest = store;
        }

        return nearest;
    }

    private async Task LoadStoreNarrationPointsAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!_storeNarrationEnabled)
            {
                _storeNarrationPoints = [];
                return;
            }

            var registrations = await _apiService.GetStoreRegistrationsAsync();
            cancellationToken.ThrowIfCancellationRequested();

            var points = new List<StoreNarrationPoint>();
            foreach (var registration in registrations)
            {
                if (!registration.Latitude.HasValue || !registration.Longitude.HasValue)
                {
                    continue;
                }

                if (registration.Latitude.Value < VinhKhanhMinLat || registration.Latitude.Value > VinhKhanhMaxLat ||
                    registration.Longitude.Value < VinhKhanhMinLng || registration.Longitude.Value > VinhKhanhMaxLng)
                {
                    continue;
                }

                var description = string.IsNullOrWhiteSpace(registration.Description)
                    ? $"Bạn đang ở gần cửa hàng {registration.StoreName}."
                    : registration.Description.Trim();
                var radiusMeters = registration.RadiusMeters is > 0 ? registration.RadiusMeters.Value : DefaultStoreNarrationRadiusMeters;

                points.Add(new StoreNarrationPoint(
                    registration.Id,
                    registration.StoreName?.Trim() ?? "Cửa hàng",
                    description,
                    registration.Latitude.Value,
                    registration.Longitude.Value,
                    radiusMeters));
            }

            _storeNarrationPoints = points;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Load store narration points failed: {ex.Message}");
            _storeNarrationPoints = new List<StoreNarrationPoint>();
        }
    }

    private void BindPoi(POI poi)
    {
        PoiBadgeLabel.Text = "ĐỊA ĐIỂM";
        LocationTitleLabel.Text = "Địa điểm đang xem";
        NowPlayingLabel.Text = poi.Name;
        AddressLabel.Text = string.IsNullOrWhiteSpace(poi.Description)
            ? "Số nhà, đường: Chưa có thông tin"
            : $"Số nhà, đường: {poi.Description}";
    }

    private static bool IsInsideVinhKhanhBounds(POI poi)
    {
        return poi.Latitude >= VinhKhanhMinLat && poi.Latitude <= VinhKhanhMaxLat &&
               poi.Longitude >= VinhKhanhMinLng && poi.Longitude <= VinhKhanhMaxLng;
    }

    private void InitializeMap(POI? poi)
    {
        LoadLeafletMap(poi, centerOnUser: true);
    }

    private void LoadLeafletMap(POI? poi, bool centerOnUser = false)
    {
        var hasInternet = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

        if (hasInternet)
        {
            var centerLat = (centerOnUser && _userLocation is not null ? _userLocation.Latitude : poi?.Latitude ?? _vinhKhanhCenter.Latitude)
                .ToString(CultureInfo.InvariantCulture);
            var centerLng = (centerOnUser && _userLocation is not null ? _userLocation.Longitude : poi?.Longitude ?? _vinhKhanhCenter.Longitude)
                .ToString(CultureInfo.InvariantCulture);
            var hasUserLocation = _userLocation is not null;
            var userLat = hasUserLocation ? _userLocation!.Latitude.ToString(CultureInfo.InvariantCulture) : "null";
            var userLng = hasUserLocation ? _userLocation!.Longitude.ToString(CultureInfo.InvariantCulture) : "null";
            var selectedPoiLat = poi?.Latitude.ToString(CultureInfo.InvariantCulture) ?? "null";
            var selectedPoiLng = poi?.Longitude.ToString(CultureInfo.InvariantCulture) ?? "null";
            var selectedPoiName = EscapeJavaScript(poi?.Name ?? "Điểm đang chọn");
            var selectedPoiDesc = EscapeJavaScript(poi?.Description ?? "Không có mô tả");
            var shouldRestoreSelection = _hasExplicitMapSelection ? "true" : "false";
            var poisScriptArray = BuildAllPoisBlock(_allPois);

            var onlineHtml = $@"<!DOCTYPE html>
<html>
<head>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no' />
    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
    <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
    <style>
        html, body {{
            height: 100%;
            width: 100%;
            margin: 0;
            padding: 0;
            overflow: hidden;
            background: #f5efe5;
        }}
        #map {{
            width: 100%;
            height: 100%;
        }}
        .leaflet-control-attribution {{
            font-size: 9px;
        }}
        .poi-popup-name {{
            font-weight: 700;
            margin-bottom: 4px;
            color: #1f2937;
        }}
        .poi-popup-desc {{
            color: #4b5563;
            font-size: 12px;
        }}
        .store-poi-icon-wrapper {{
            background: transparent;
            border: 0;
        }}
        .store-poi-icon {{
            width: 18px;
            height: 18px;
            border-radius: 999px;
            background: #f97316;
            border: 2px solid #ffffff;
            box-shadow: 0 4px 12px rgba(15, 23, 42, 0.35);
        }}
        .poi-name-label {{
            background: #111827;
            border: 0;
            color: #ffffff;
            font-size: 11px;
            font-weight: 700;
            padding: 4px 8px;
            border-radius: 999px;
            box-shadow: 0 4px 10px rgba(0, 0, 0, 0.25);
        }}
        .poi-name-label:before {{
            display: none;
        }}
    </style>
</head>
<body>
    <div id='map'></div>
    <script>
        const centerLat = {centerLat};
        const centerLng = {centerLng};
        const userLat = {userLat};
        const userLng = {userLng};
        const selectedPoiLat = {selectedPoiLat};
        const selectedPoiLng = {selectedPoiLng};
        const selectedPoiName = '{selectedPoiName}';
        const selectedPoiDesc = '{selectedPoiDesc}';
        const shouldRestoreSelection = {shouldRestoreSelection};
        const pois = {poisScriptArray};
        const storeIcon = L.divIcon({{
            className: 'store-poi-icon-wrapper',
            html: '<div class=""store-poi-icon""></div>',
            iconSize: [18, 18],
            iconAnchor: [9, 9]
        }});

        const map = L.map('map', {{
            zoomControl: false,
            minZoom: 15,
            maxZoom: 19,
            maxBounds: [[{VinhKhanhMinLat.ToString(CultureInfo.InvariantCulture)}, {VinhKhanhMinLng.ToString(CultureInfo.InvariantCulture)}], [{VinhKhanhMaxLat.ToString(CultureInfo.InvariantCulture)}, {VinhKhanhMaxLng.ToString(CultureInfo.InvariantCulture)}]],
            maxBoundsViscosity: 1.0
        }}).setView([centerLat, centerLng], {LeafletZoom.ToString(CultureInfo.InvariantCulture)});

        // Force zoom buttons to top-left as requested.
        L.control.zoom({{ position: 'topleft' }}).addTo(map);

        L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
            attribution: '&copy; OpenStreetMap contributors'
        }}).addTo(map);

        const bounds = {{
            minLat: {VinhKhanhMinLat.ToString(CultureInfo.InvariantCulture)},
            maxLat: {VinhKhanhMaxLat.ToString(CultureInfo.InvariantCulture)},
            minLng: {VinhKhanhMinLng.ToString(CultureInfo.InvariantCulture)},
            maxLng: {VinhKhanhMaxLng.ToString(CultureInfo.InvariantCulture)}
        }};

        function clampToBounds(lat, lng) {{
            return {{
                lat: Math.min(bounds.maxLat, Math.max(bounds.minLat, lat)),
                lng: Math.min(bounds.maxLng, Math.max(bounds.minLng, lng))
            }};
        }}

        const routeStart = (userLat !== null && userLng !== null)
            ? clampToBounds(userLat, userLng)
            : clampToBounds(centerLat, centerLng);

        if (userLat !== null && userLng !== null) {{
            const userOnMap = clampToBounds(userLat, userLng);
            L.circleMarker([userOnMap.lat, userOnMap.lng], {{
                radius: 8,
                color: '#ffffff',
                weight: 2,
                fillColor: '#0ea5e9',
                fillOpacity: 1
            }}).addTo(map).bindTooltip('Vị trí của bạn', {{ direction: 'top', offset: [0, -6] }});
        }}

        let activeRoute = null;
        let activeDestinationDot = null;

        function distanceMeters(a, b) {{
            const toRad = v => (v * Math.PI) / 180;
            const dLat = toRad(b.lat - a.lat);
            const dLng = toRad(b.lng - a.lng);
            const lat1 = toRad(a.lat);
            const lat2 = toRad(b.lat);
            const hav = Math.sin(dLat / 2) ** 2 + Math.cos(lat1) * Math.cos(lat2) * Math.sin(dLng / 2) ** 2;
            return 6371000 * 2 * Math.atan2(Math.sqrt(hav), Math.sqrt(1 - hav));
        }}

        async function routeTo(lat, lng) {{
            const target = clampToBounds(lat, lng);

            if (activeRoute) {{
                map.removeLayer(activeRoute);
            }}
            if (activeDestinationDot) {{
                map.removeLayer(activeDestinationDot);
            }}

            activeDestinationDot = L.circleMarker([target.lat, target.lng], {{
                radius: 7,
                color: '#0f766e',
                weight: 2,
                fillColor: '#14b8a6',
                fillOpacity: 0.95
            }}).addTo(map);

            if (distanceMeters(routeStart, target) < 5) {{
                map.setView([target.lat, target.lng], 17);
                return;
            }}

            try {{
                const routeUrl = `https://router.project-osrm.org/route/v1/driving/${{routeStart.lng}},${{routeStart.lat}};${{target.lng}},${{target.lat}}?overview=full&geometries=geojson`;
                const response = await fetch(routeUrl);
                const data = await response.json();

                if (response.ok && data && data.code === 'Ok' && data.routes && data.routes.length > 0) {{
                    const coords = data.routes[0].geometry.coordinates.map(c => [c[1], c[0]]);
                    activeRoute = L.polyline(coords, {{
                        color: '#0f766e',
                        weight: 5,
                        opacity: 0.95
                    }}).addTo(map);
                }} else {{
                    throw new Error('OSRM route unavailable');
                }}
            }} catch {{
                // Fallback when routing service is unreachable on emulator/network.
                activeRoute = L.polyline([
                    [routeStart.lat, routeStart.lng],
                    [target.lat, target.lng]
                ], {{
                    color: '#0f766e',
                    weight: 5,
                    opacity: 0.9,
                    dashArray: '8, 8'
                }}).addTo(map);
            }}

            map.fitBounds(activeRoute.getBounds(), {{ padding: [36, 36], maxZoom: 17 }});
        }}

        function buildAddressText(address) {{
            if (!address) {{
                return 'Chưa có thông tin';
            }}

            const house = address.house_number || '';
            const road = address.road || address.pedestrian || address.footway || address.path || '';
            const combined = `${{house}} ${{road}}`.trim();
            return combined || road || 'Chưa có thông tin';
        }}

        async function resolvePlaceInfo(lat, lng, fallbackName, fallbackDesc) {{
            try {{
                const reverseUrl = `https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat=${{lat}}&lon=${{lng}}&zoom=18&addressdetails=1`;
                const response = await fetch(reverseUrl, {{ headers: {{ 'Accept': 'application/json' }} }});
                if (!response.ok) {{
                    throw new Error('reverse geocode failed');
                }}

                const data = await response.json();
                const name = (data.name || fallbackName || 'Địa điểm đã chọn').trim();
                const addr = buildAddressText(data.address);
                return {{ name, address: addr }};
            }} catch {{
                return {{
                    name: (fallbackName || 'Địa điểm đã chọn').trim(),
                    address: (fallbackDesc || 'Chưa có thông tin').trim()
                }};
            }}
        }}

        function notifySelection(item, addressText) {{
            const name = encodeURIComponent(item.name || 'Địa điểm đã chọn');
            const desc = encodeURIComponent(item.desc || 'Chưa có thông tin');
            const addr = encodeURIComponent(addressText || item.desc || 'Chưa có thông tin');
            const lat = encodeURIComponent(item.lat);
            const lng = encodeURIComponent(item.lng);
            setTimeout(() => {{
                window.location.href = `app://selected?name=${{name}}&desc=${{desc}}&addr=${{addr}}&lat=${{lat}}&lng=${{lng}}`;
            }}, 0);
        }}

        function addPoiMarker(item, focus) {{
            const popup = `<div class='poi-popup-name'>${{item.name}}</div><div class='poi-popup-desc'>${{item.desc}}</div>`;
            const marker = L.marker([item.lat, item.lng], {{ icon: storeIcon, riseOnHover: true }})
                .addTo(map)
                .bindPopup(popup)
                .bindTooltip(item.name, {{
                    permanent: true,
                    direction: 'top',
                    offset: [0, -14],
                    className: 'poi-name-label'
                }});

            const onSelect = async function() {{
                await routeTo(item.lat, item.lng);
                const info = await resolvePlaceInfo(item.lat, item.lng, item.name, item.desc);
                notifySelection(item, info.address);
            }};

            marker.on('click', onSelect);
            marker.on('touchstart', onSelect);

            if (focus) {{
                marker.openPopup();
            }}
        }}

        pois.forEach(p => addPoiMarker(p, false));

        map.on('click', async function(e) {{
            const item = {{
                lat: e.latlng.lat,
                lng: e.latlng.lng,
                name: 'Địa điểm đã chọn',
                desc: 'Chưa có thông tin'
            }};
            await routeTo(item.lat, item.lng);
            const info = await resolvePlaceInfo(item.lat, item.lng, item.name, item.desc);
            item.name = info.name;
            notifySelection(item, info.address);
        }});

        if (shouldRestoreSelection && selectedPoiLat !== null && selectedPoiLng !== null) {{
            const selectedItem = {{
                lat: selectedPoiLat,
                lng: selectedPoiLng,
                name: selectedPoiName,
                desc: selectedPoiDesc
            }};
            addPoiMarker(selectedItem, true);
            routeTo(selectedPoiLat, selectedPoiLng);
        }}
    </script>
</body>
</html>";

            LeafletMapView.Source = new HtmlWebViewSource
            {
                Html = onlineHtml
            };

            return;
        }

        var html = $@"<!DOCTYPE html>
<html>
<head>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no' />
    <style>
        html, body {{
            height: 100%;
            width: 100%;
            margin: 0;
            padding: 0;
            overflow: hidden;
            background: #efe9dc;
            font-family: -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif;
        }}
        .offline-shell {{
            height: 100%;
            width: 100%;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 20px;
            box-sizing: border-box;
        }}
        .offline-card {{
            width: 100%;
            border-radius: 16px;
            background: #fffaf2;
            border: 1px solid #ecdcc7;
            padding: 16px;
            color: #5b4631;
            line-height: 1.45;
        }}
        .title {{
            font-size: 16px;
            font-weight: 700;
            color: #7a3e12;
            margin-bottom: 10px;
        }}
        .meta {{
            font-size: 13px;
        }}
    </style>
</head>
<body>
    <div class='offline-shell'>
        <div class='offline-card'>
            <div class='title'>Khong tai duoc ban do online</div>
            <div class='meta'>
                Khong co ket noi Internet tren thiet bi/emulator.<br/>
                Khu vuc mac dinh: Pho am thuc Vinh Khanh (Q4, TP.HCM).<br/>
                Lat: {VinhKhanhMinLat.ToString(CultureInfo.InvariantCulture)} - {VinhKhanhMaxLat.ToString(CultureInfo.InvariantCulture)}<br/>
                Lng: {VinhKhanhMinLng.ToString(CultureInfo.InvariantCulture)} - {VinhKhanhMaxLng.ToString(CultureInfo.InvariantCulture)}
            </div>
        </div>
    </div>
</body>
</html>";

        LeafletMapView.Source = new HtmlWebViewSource
        {
            Html = html
        };
    }

    private static string BuildAllPoisBlock(IEnumerable<POI> pois)
    {
        var entries = pois.Select(poi =>
            $"{{lat:{poi.Latitude.ToString(CultureInfo.InvariantCulture)},lng:{poi.Longitude.ToString(CultureInfo.InvariantCulture)},name:'{EscapeJavaScript(poi.Name)}',desc:'{EscapeJavaScript(poi.Description ?? "Không có mô tả")}'}}");

        return $"[{string.Join(",", entries)}]";
    }

    private static string EscapeJavaScript(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\r", " ")
            .Replace("\n", " ");
    }

    private static async Task<Location?> TryGetUserLocationAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            const double acceptableAccuracyMeters = 75;
            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(6));

            var lastKnown = await Geolocation.Default.GetLastKnownLocationAsync();
            if (IsAcceptableLocation(lastKnown, acceptableAccuracyMeters) &&
                (lastKnown is null || DateTimeOffset.UtcNow - lastKnown.Timestamp <= TimeSpan.FromMinutes(5)))
            {
                return lastKnown;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var current = await Geolocation.Default.GetLocationAsync(request);
            if (IsAcceptableLocation(current, acceptableAccuracyMeters))
            {
                return current;
            }

            return current ?? lastKnown;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsAcceptableLocation(Location? location, double maxAccuracyMeters)
    {
        if (location is null)
        {
            return false;
        }

        if (location.Accuracy is null)
        {
            return true;
        }

        return location.Accuracy.Value <= maxAccuracyMeters;
    }

    private void StartRealtimeTracking()
    {
        if (_trackingCts is not null)
        {
            return;
        }

        _trackingCts = new CancellationTokenSource();
        _trackingTask = Task.Run(() => RealtimeTrackingLoopAsync(_trackingCts.Token));
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
        _trackingTask = null;
    }

    private async Task RealtimeTrackingLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!_autoPlayEnabled || (_allPois.Count == 0 && (!_storeNarrationEnabled || _storeNarrationPoints.Count == 0)))
                {
                    await Task.Delay(GetTrackingInterval(), cancellationToken);
                    continue;
                }

                var latestLocation = await TryGetUserLocationAsync(cancellationToken);
                if (latestLocation is null)
                {
                    await Task.Delay(GetTrackingInterval(), cancellationToken);
                    continue;
                }

                _userLocation = latestLocation;

                if (_storeNarrationEnabled)
                {
                    var candidateStore = FindNearestStoreInRange(latestLocation);
                    if (candidateStore is not null)
                    {
                        var narratedStore = await _locationService.NarrateTextAsync(
                            $"store:{candidateStore.Id}",
                            candidateStore.Description,
                            "vi",
                            cancellationToken);

                        if (narratedStore)
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                PoiBadgeLabel.Text = "CỬA HÀNG";
                                LocationTitleLabel.Text = "Cửa hàng gần bạn";
                                NowPlayingLabel.Text = candidateStore.Name;
                                AddressLabel.Text = candidateStore.Description;
                            });
                        }

                        await Task.Delay(GetTrackingInterval(), cancellationToken);
                        continue;
                    }
                }

                var candidatePoi = _locationService.FindBestPoiInRange(latestLocation, _allPois);

                if (candidatePoi is not null)
                {
                    var narrated = await _locationService.NarratePoiAsync(candidatePoi, cancellationToken);
                    if (narrated)
                    {
                        _currentPoi = candidatePoi;
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            BindPoi(candidatePoi);
                            PoiBadgeLabel.Text = "TỰ ĐỘNG";
                        });

                    }
                }

                await Task.Delay(GetTrackingInterval(), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(GetTrackingInterval(), cancellationToken);
            }
        }
    }

    private TimeSpan GetTrackingInterval()
    {
        return _batteryOptimized ? BatteryTrackingInterval : NormalTrackingInterval;
    }

    private async void OnMenuTapped(object? sender, TappedEventArgs e)
    {
        await ToggleSidebarAsync();
    }

    private async Task ToggleSidebarAsync()
    {
        if (_isSidebarOpen)
        {
            await CloseSidebarAsync();
            return;
        }

        await OpenSidebarAsync();
    }

    private async Task OpenSidebarAsync()
    {
        if (_sidebarAnimating || _isSidebarOpen)
        {
            return;
        }

        _sidebarAnimating = true;
        SidebarBackdrop.IsVisible = true;
        SidebarBackdrop.InputTransparent = false;

        var backdropTask = SidebarBackdrop.FadeToAsync(1, 180, Easing.CubicOut);
        var panelTask = SidebarPanel.TranslateToAsync(0, 0, 220, Easing.CubicOut);
        await Task.WhenAll(backdropTask, panelTask);

        _isSidebarOpen = true;
        _sidebarAnimating = false;
    }

    private async Task CloseSidebarAsync()
    {
        if (_sidebarAnimating || !_isSidebarOpen)
        {
            return;
        }

        _sidebarAnimating = true;

        var backdropTask = SidebarBackdrop.FadeToAsync(0, 180, Easing.CubicIn);
        var panelTask = SidebarPanel.TranslateToAsync(-340, 0, 220, Easing.CubicIn);
        await Task.WhenAll(backdropTask, panelTask);

        SidebarBackdrop.InputTransparent = true;
        SidebarBackdrop.IsVisible = false;
        _isSidebarOpen = false;
        _sidebarAnimating = false;
    }

    private async void OnSidebarBackdropTapped(object? sender, TappedEventArgs e)
    {
        await CloseSidebarAsync();
    }

    private async void OnCloseSidebarTapped(object? sender, TappedEventArgs e)
    {
        await CloseSidebarAsync();
    }

    private async void OnSidebarHomeClicked(object? sender, EventArgs e)
    {
        await CloseSidebarAsync();
    }

    private async void OnSidebarMenuClicked(object? sender, EventArgs e)
    {
        await CloseSidebarAsync();
        await _navigator.ShowMenuAsync();
    }

    private async void OnSidebarProfileClicked(object? sender, EventArgs e)
    {
        await CloseSidebarAsync();
        await _navigator.ShowProfileAsync();
    }

    private async void OnSidebarRecenterClicked(object? sender, EventArgs e)
    {
        await CloseSidebarAsync();
        await RecenterCurrentUserAsync();
    }

    private async void OnProfileTapped(object? sender, TappedEventArgs e)
    {
        if (_isSidebarOpen)
        {
            await CloseSidebarAsync();
        }

        await _navigator.ShowProfileAsync();
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
            : "Chưa có thông tin";

        var address = query.TryGetValue("addr", out var selectedAddr)
            ? selectedAddr
            : description;

        var lat = query.TryGetValue("lat", out var selectedLat) ? selectedLat : string.Empty;
        var lng = query.TryGetValue("lng", out var selectedLng) ? selectedLng : string.Empty;

        if (double.TryParse(lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLat) &&
            double.TryParse(lng, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLng))
        {
            _hasExplicitMapSelection = true;

            var isSameSelection = _selectedLat.HasValue &&
                                  _selectedLng.HasValue &&
                                  Math.Abs(_selectedLat.Value - parsedLat) < 0.000001 &&
                                  Math.Abs(_selectedLng.Value - parsedLng) < 0.000001 &&
                                  string.Equals(_selectedName, name, StringComparison.Ordinal) &&
                                  string.Equals(_selectedDescription, address, StringComparison.Ordinal);

            _selectedLat = parsedLat;
            _selectedLng = parsedLng;
            _selectedName = name;
            _selectedDescription = address;

            if (!isSameSelection)
            {
                SaveTripSelection(name, address);
            }
        }

        PoiBadgeLabel.Text = "ĐỊA ĐIỂM";
        LocationTitleLabel.Text = "Địa điểm đã chọn";
        NowPlayingLabel.Text = name;
        AddressLabel.Text = $"Số nhà, đường: {address}";
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

    private static void SaveTripSelection(string name, string description)
    {
        var selectedTrip = new TripHistoryItem
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Điểm đã chọn" : name,
            Description = string.IsNullOrWhiteSpace(description) ? "Không có mô tả" : description,
            CreatedAtUtc = DateTime.UtcNow
        };

        Preferences.Default.Set(TripsHistoryKey, JsonSerializer.Serialize(selectedTrip));
    }

    private static TripHistoryItem? LoadTripHistory()
    {
        var json = Preferences.Default.Get(TripsHistoryKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var selectedTrip = JsonSerializer.Deserialize<TripHistoryItem>(json);
            if (selectedTrip is not null)
            {
                return selectedTrip;
            }

            var legacyTrips = JsonSerializer.Deserialize<List<TripHistoryItem>>(json);
            return legacyTrips?
                .OrderByDescending(item => item.CreatedAtUtc)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private sealed class TripHistoryItem
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
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
        await RecenterCurrentUserAsync();
    }

    private async Task RecenterCurrentUserAsync()
    {
        // Prevent repeated rapid taps causing back-to-back geolocation requests.
        if (DateTime.UtcNow - _lastRecenterAt < TimeSpan.FromSeconds(1))
        {
            return;
        }

        _lastRecenterAt = DateTime.UtcNow;
        _userLocation = await TryGetUserLocationAsync(CancellationToken.None);

        if (_userLocation is null)
        {
            await DisplayAlertAsync("Vị trí", "Không lấy được vị trí hiện tại của bạn.", "OK");
            return;
        }

        await RecenterMapAsync(_userLocation);
    }

    private async Task RecenterMapAsync(Location location)
    {
        LoadLeafletMap(_currentPoi, centerOnUser: true);
        await Task.CompletedTask;
    }

    private void OnAutoPlayToggled(object? sender, ToggledEventArgs e)
    {
        _autoPlayEnabled = e.Value;
        PoiBadgeLabel.Text = e.Value ? "TỰ ĐỘNG" : "THỦ CÔNG";
    }

    private void OnBatteryOptimizedToggled(object? sender, ToggledEventArgs e)
    {
        _batteryOptimized = e.Value;
        var text = _batteryOptimized ? "Chế độ pin: Tối ưu" : "Chế độ pin: Bình thường";
        PoiBadgeLabel.Text = text.ToUpperInvariant();
    }

    private void LoadFeatureSettings()
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

            _autoPlayEnabled = data.AutoPlay;
            _batteryOptimized = data.BatteryOptimized;
            _storeNarrationEnabled = data.StoreNarrationEnabled;
        }
        catch
        {
            // Ignore invalid settings.
        }
    }

    private sealed class SettingData
    {
        public bool AutoPlay { get; set; }
        public bool BatteryOptimized { get; set; }
        public bool NotificationEnabled { get; set; }
        public bool StoreNarrationEnabled { get; set; } = true;
    }

    private sealed class StoreNarrationPoint
    {
        public StoreNarrationPoint(int id, string name, string description, double latitude, double longitude, double radiusMeters)
        {
            Id = id;
            Name = name;
            Description = description;
            Latitude = latitude;
            Longitude = longitude;
            RadiusMeters = radiusMeters;
        }

        public int Id { get; }
        public string Name { get; }
        public string Description { get; }
        public double Latitude { get; }
        public double Longitude { get; }
        public double RadiusMeters { get; }
    }

    private sealed class NominatimGeoResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("lat")]
        public string Lat { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("lon")]
        public string Lon { get; set; } = string.Empty;
    }
}
