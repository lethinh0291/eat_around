using MobileApp.Services;
using SharedLib.Models;
using System.Globalization;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;
using System.Threading;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace ZesTour.Views;

public partial class MainPage : ContentPage
{
    private readonly DatabaseService _databaseService;
    private readonly ApiService _apiService;
    private readonly LocationService _locationService;
    private readonly AppNavigator _navigator;
    private readonly Location _defaultLocation = new(10.7724, 106.6981);
    private readonly Location _vinhKhanhCenter = new(10.7589, 106.7072);
    private const double VinhKhanhRadiusMeters = 1200;
    private const double VinhKhanhMinLat = 10.7530;
    private const double VinhKhanhMaxLat = 10.7648;
    private const double VinhKhanhMinLng = 106.6992;
    private const double VinhKhanhMaxLng = 106.7152;
    private const double LeafletZoom = 16;
    private const string TripsHistoryKey = "zes_trip_history_v1";
    private bool _autoPlayEnabled = true;
    private bool _batteryOptimized;
    private bool _isSidebarOpen;
    private bool _sidebarAnimating;
    private bool _dataLoaded;
    private CancellationTokenSource? _loadingCts;
    private DateTime _lastRecenterAt = DateTime.MinValue;
    private CancellationTokenSource? _trackingCts;
    private Task? _trackingTask;
    private List<POI> _allPois = new();
    private POI? _currentPoi;
    private Location? _userLocation;
    private double? _selectedLat;
    private double? _selectedLng;
    private string _selectedName = string.Empty;
    private string _selectedDescription = string.Empty;
    private static readonly TimeSpan NormalTrackingInterval = TimeSpan.FromSeconds(7);
    private static readonly TimeSpan BatteryTrackingInterval = TimeSpan.FromSeconds(12);

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

        _userLocation = await locationTask;
        cancellationToken.ThrowIfCancellationRequested();

        _currentPoi = FindNearestPoi(_allPois);

        if (_currentPoi is null)
        {
            MapPoiLabel.Text = "Bản đồ quanh bạn";
            PoiBadgeLabel.Text = "KHÔNG CÓ DỮ LIỆU";
            NowPlayingLabel.Text = "Đang hiển thị bản đồ mặc định.";
            InitializeMap(null);
            StartRealtimeTracking();
            return;
        }

        BindPoi(_currentPoi);
        InitializeMap(_currentPoi);

        // Smoothly move to the user position if available after the map is ready.
        if (_userLocation is not null)
        {
            await RecenterMapAsync(_userLocation);
        }

        StartRealtimeTracking();
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

    private static bool IsInsideVinhKhanhBounds(POI poi)
    {
        return poi.Latitude >= VinhKhanhMinLat && poi.Latitude <= VinhKhanhMaxLat &&
               poi.Longitude >= VinhKhanhMinLng && poi.Longitude <= VinhKhanhMaxLng;
    }



    private void BindPoi(POI poi)
    {
        MapPoiLabel.Text = poi.Name;
        PoiBadgeLabel.Text = string.IsNullOrWhiteSpace(poi.LanguageCode) ? "POI" : poi.LanguageCode.ToUpperInvariant();
        NowPlayingLabel.Text = $"ĐANG PHÁT: {poi.Name} (Nội dung tự động)";
    }

    private void InitializeMap(POI? poi)
    {
        LoadLeafletMap(poi, centerOnUser: true);
    }

    private void LoadLeafletMap(POI? poi, bool centerOnUser = false)
    {
        var center = centerOnUser && _userLocation is not null ? _userLocation : _vinhKhanhCenter;
        var hasUser = _userLocation is not null &&
                      (centerOnUser || _locationService.CalculateDistance(center.Latitude, center.Longitude, _userLocation.Latitude, _userLocation.Longitude) <= VinhKhanhRadiusMeters);
        var userLatitude = _userLocation?.Latitude.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        var userLongitude = _userLocation?.Longitude.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        var allPoisBlock = BuildAllPoisBlock(_allPois);
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
            display: block;
            opacity: 0;
            transition: opacity 0.25s ease;
        }
        #fallback {
            height: 100%;
            width: 100%;
            border: 0;
            display: block;
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
        const vinhKhanhBounds = L.latLngBounds([__MIN_LAT__, __MIN_LNG__], [__MAX_LAT__, __MAX_LNG__]);

        if (typeof L === 'undefined') {
            document.getElementById('fallback').style.display = 'block';
        } else {
            const fallback = document.getElementById('fallback');
            const mapElement = document.getElementById('map');
            let tilesLoaded = false;

            function activateMap() {
                if (mapElement.style.opacity === '1') {
                    return;
                }

                mapElement.style.opacity = '1';
                fallback.style.display = 'none';
            }

            const map = L.map('map', {
                zoomControl: true,
                attributionControl: false,
                touchZoom: true,
                doubleClickZoom: true,
                scrollWheelZoom: true,
                dragging: true
            });

            map.setMaxBounds(vinhKhanhBounds.pad(0.01));

            const tileLayer = L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png', {
                maxZoom: 19
            });

            tileLayer.on('load', () => {
                tilesLoaded = true;
                activateMap();
            });

            tileLayer.on('tileerror', () => {
                fallback.style.display = 'block';
            });

            tileLayer.addTo(map);

            setTimeout(() => {
                if (!tilesLoaded) {
                    fallback.style.display = 'block';
                }
            }, 5000);

            map.setView(streetCenter, __LEAFLET_ZOOM__);
            map.fitBounds(vinhKhanhBounds, { padding: [12, 12], maxZoom: 16 });

            let userPos = __USER_POSITION__;
            let userMarker = null;
            let destinationMarker = null;
            let routeLine = null;

            function updateUserMarker(lat, lng) {
                const userLatLng = [lat, lng];
                userPos = [lat, lng];

                if (!userMarker) {
                    const userIcon = L.divIcon({ className: 'user-dot', html: '<div></div>', iconSize: [16,16], iconAnchor:[8,8] });
                    userMarker = L.marker(userLatLng, { icon: userIcon }).addTo(map).bindPopup('Bạn đang ở đây');
                    return;
                }

                userMarker.setLatLng(userLatLng);
            }

window.recenterToUser = function(lat, lng)
{
    updateUserMarker(lat, lng);
    map.panTo([lat, lng], { animate: true });
};

function clearRoute()
{
    if (routeLine)
    {
        map.removeLayer(routeLine);
        routeLine = null;
    }
    if (destinationMarker)
    {
        map.removeLayer(destinationMarker);
        destinationMarker = null;
    }
}

function notifySelection(name, desc, lat, lng)
{
    const url = 'app://selected?name=' + encodeURIComponent(name)
        + '&desc=' + encodeURIComponent(desc)
        + '&lat=' + encodeURIComponent(lat)
        + '&lng=' + encodeURIComponent(lng);
    window.location.href = url;
}

async function drawRouteTo(destLat, destLng, label, detail)
{
    clearRoute();

    notifySelection(label, detail, destLat, destLng);

    destinationMarker = L.marker([destLat, destLng]).addTo(map);
    destinationMarker.bindPopup('<b>' + label + '</b>').openPopup();

    try
    {
        const url = `https://router.project-osrm.org/route/v1/__ROUTE_PROFILE__/${userPos[1]},${userPos[0]};${destLng},${destLat}?overview=full&geometries=geojson`;
        const response = await fetch(url);
        const data = await response.json();

        if (data && data.code === 'Ok' && data.routes && data.routes.length > 0)
        {
            const coords = data.routes[0].geometry.coordinates.map(c => [c[1], c[0]]);
            routeLine = L.polyline(coords, { color: '__ROUTE_COLOR__', weight: 5, opacity: 0.9, dashArray: '__ROUTE_DASH__' }).addTo(map);
        }
        else
        {
            routeLine = L.polyline([userPos, [destLat, destLng]], { color: '__ROUTE_COLOR__', weight: 4, dashArray: '__ROUTE_DASH__' }).addTo(map);
        }
    }
    catch
    {
        routeLine = L.polyline([userPos, [destLat, destLng]], { color: '__ROUTE_COLOR__', weight: 4, dashArray: '__ROUTE_DASH__' }).addTo(map);
    }

    if (routeLine)
    {
        map.fitBounds(routeLine.getBounds(), { padding: [28, 28], maxZoom: 18 });
    }
}

async function resolvePlaceInfo(lat, lng)
{
    const fallback = {
                    label: 'Điểm đã chọn',
                    detail: `Tọa độ: ${ lat.toFixed(6)}, ${ lng.toFixed(6)}`
                };

function buildLabelAndDetail(rawName, rawDetail, isWorship = false)
{
    let label = (rawName || '').trim();
    if (!label)
    {
        return fallback;
    }

    if (isWorship && !label.toLowerCase().includes('nhà thờ'))
    {
        label = `Nhà thờ ${ label}`;
    }

    return {
        label,
                        detail: (rawDetail || fallback.detail).trim()
                    }
    ;
}

                try {
                    const url = `https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat=${lat}&lon=${lng}&zoom=18&addressdetails=1`;
                    const response = await fetch(url, {
                        headers: {
                            'Accept': 'application/json'
                        }
                    });

if (!response.ok)
{
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
if (label)
{
    return buildLabelAndDetail(label, data.display_name, isWorship);
}
                } catch {
    // Try another provider if Nominatim fails.
}

try
{
    const photonUrl = `https://photon.komoot.io/reverse?lat=${lat}&lon=${lng}`;
    const photonRes = await fetch(photonUrl, {
    headers:
        {
            'Accept': 'application/json'
                        }
    });

    if (photonRes.ok)
    {
        const photon = await photonRes.json();
        const feature = photon.features && photon.features.length > 0 ? photon.features[0] : null;
        const p = feature ? (feature.properties || { }) : { }
        ;
        const pLabel = p.name || p.street || p.housenumber || p.city || p.county;
        const pDetail = [p.street, p.district, p.city, p.state, p.country]
            .filter(Boolean)
            .join(', ');

        if (pLabel)
        {
            return buildLabelAndDetail(pLabel, pDetail);
        }
    }
}
catch
{
    // Ignore and use fallback.
}

return fallback;
            }

            const streetIcon = L.divIcon({
                className: '',
                html: '<div class=""street-pin"">Phố Ẩm Thực Vĩnh Khánh</div>',
                iconSize: [160, 34],
                iconAnchor: [80, 34]
            });
const streetMarker = L.marker(streetCenter, { icon: streetIcon }).addTo(map);
streetMarker.on('click', () => drawRouteTo(streetCenter[0], streetCenter[1], 'Phố Ẩm Thực Vĩnh Khánh', 'Phường 10, Quận 4, TP.HCM'));

__USER_BLOCK__

__ALL_POIS_BLOCK__

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
</html> ";

        html = html
            .Replace("__CENTER_LAT__", center.Latitude.ToString(CultureInfo.InvariantCulture))
            .Replace("__CENTER_LNG__", center.Longitude.ToString(CultureInfo.InvariantCulture))
            .Replace("__MIN_LAT__", VinhKhanhMinLat.ToString(CultureInfo.InvariantCulture))
            .Replace("__MAX_LAT__", VinhKhanhMaxLat.ToString(CultureInfo.InvariantCulture))
            .Replace("__MIN_LNG__", VinhKhanhMinLng.ToString(CultureInfo.InvariantCulture))
            .Replace("__MAX_LNG__", VinhKhanhMaxLng.ToString(CultureInfo.InvariantCulture))
            .Replace("__LEAFLET_ZOOM__", LeafletZoom.ToString(CultureInfo.InvariantCulture))
            .Replace("__ROUTE_PROFILE__", routeProfile)
            .Replace("__ROUTE_COLOR__", routeColor)
            .Replace("__ROUTE_DASH__", routeDashArray)
            .Replace("__USER_POSITION__", hasUser ? $"[{userLatitude}, {userLongitude}]" : "streetCenter")
            .Replace("__USER_BLOCK__", hasUser
                ? $"updateUserMarker({userLatitude}, {userLongitude});"
                : string.Empty)
            .Replace("__ALL_POIS_BLOCK__", allPoisBlock)
            .Replace("__SELECTED_ROUTE_BLOCK__", _selectedLat.HasValue && _selectedLng.HasValue
                ? $"drawRouteTo({_selectedLat.Value.ToString(CultureInfo.InvariantCulture)}, {_selectedLng.Value.ToString(CultureInfo.InvariantCulture)}, '{EscapeJavaScript(_selectedName)}', '{EscapeJavaScript(_selectedDescription)}');"
                : string.Empty);

        LeafletMapView.Source = new HtmlWebViewSource
        {
            Html = html
        };
    }

    private string BuildAllPoisBlock(IEnumerable<POI> pois)
    {
        var sb = new StringBuilder();
        sb.AppendLine("const poiCoords = [];");

        foreach (var poi in pois)
        {
            if (double.IsNaN(poi.Latitude) || double.IsNaN(poi.Longitude))
            {
                continue;
            }

            var lat = poi.Latitude.ToString(CultureInfo.InvariantCulture);
            var lng = poi.Longitude.ToString(CultureInfo.InvariantCulture);
            var name = EscapeJavaScript(string.IsNullOrWhiteSpace(poi.Name) ? "POI" : poi.Name);
            var desc = EscapeJavaScript(string.IsNullOrWhiteSpace(poi.Description) ? "Không có mô tả." : poi.Description);

            sb.AppendLine($@"(function() {{
                const lat = {lat};
                const lng = {lng};
                const name = '{name}';
                const desc = '{desc}';
                const poiIcon = L.divIcon({{
                    className: 'poi-label',
                    html: '<div style=""background: #FFF7ED; color: #1B2430; padding: 4px 8px; border-radius: 4px; font-size: 11px; font-weight: bold; white-space: nowrap; box-shadow: 0 2px 4px rgba(0,0,0,0.2);"">'' + name + ''</div>',
                    iconSize: [120, 24],
                    iconAnchor: [60, 24]
                }});
                const marker = L.marker([lat, lng], {{ icon: poiIcon }}).addTo(map).bindPopup('<b>' + name + '</b><br/>' + desc);
                marker.on('click', () => drawRouteTo(lat, lng, name, desc));
                poiCoords.push([lat, lng]);
            }})();");
        }

        sb.AppendLine("if (poiCoords.length > 0) { const poiBounds = L.latLngBounds(poiCoords); map.fitBounds(poiBounds.pad(0.15), { padding: [24, 24], maxZoom: 17 }); }");

        return sb.ToString();
    }

    private static string EscapeJavaScript(string value)
    {
        return value
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\r", string.Empty)
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
                if (!_autoPlayEnabled || _allPois.Count == 0)
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

                        SaveTripSelection(candidatePoi.Name, candidatePoi.Description ?? "Không có mô tả");
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
            : "Không có mô tả";

        var lat = query.TryGetValue("lat", out var selectedLat) ? selectedLat : string.Empty;
        var lng = query.TryGetValue("lng", out var selectedLng) ? selectedLng : string.Empty;

        if (double.TryParse(lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLat) &&
            double.TryParse(lng, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLng))
        {
            var isSameSelection = _selectedLat.HasValue &&
                                  _selectedLng.HasValue &&
                                  Math.Abs(_selectedLat.Value - parsedLat) < 0.000001 &&
                                  Math.Abs(_selectedLng.Value - parsedLng) < 0.000001 &&
                                  string.Equals(_selectedName, name, StringComparison.Ordinal) &&
                                  string.Equals(_selectedDescription, description, StringComparison.Ordinal);

            _selectedLat = parsedLat;
            _selectedLng = parsedLng;
            _selectedName = name;
            _selectedDescription = description;

            if (!isSameSelection)
            {
                SaveTripSelection(name, description);
            }
        }

        PoiBadgeLabel.Text = "ĐÃ CHỌN";
        NowPlayingLabel.Text = $"Đang phát thông tin liên quan đến: {name}";
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

    private static void SaveTripSelection(string name, string description)
    {
        var items = LoadTripHistory();
        items.Add(new TripHistoryItem
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Điểm đã chọn" : name,
            Description = string.IsNullOrWhiteSpace(description) ? "Không có mô tả" : description,
            CreatedAtUtc = DateTime.UtcNow
        });

        if (items.Count > 100)
        {
            items = items
                .OrderByDescending(item => item.CreatedAtUtc)
                .Take(100)
                .ToList();
        }

        Preferences.Default.Set(TripsHistoryKey, JsonSerializer.Serialize(items));
    }

    private static List<TripHistoryItem> LoadTripHistory()
    {
        var json = Preferences.Default.Get(TripsHistoryKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<TripHistoryItem>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<TripHistoryItem>>(json) ?? new List<TripHistoryItem>();
        }
        catch
        {
            return new List<TripHistoryItem>();
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
        try
        {
            var script = $"window.recenterToUser({location.Latitude.ToString(CultureInfo.InvariantCulture)}, {location.Longitude.ToString(CultureInfo.InvariantCulture)});";
            await LeafletMapView.EvaluateJavaScriptAsync(script);
        }
        catch
        {
            LoadLeafletMap(_currentPoi, centerOnUser: true);
        }
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

    private void OnAutoPlayToggled(object? sender, ToggledEventArgs e)
    {
        _autoPlayEnabled = e.Value;
        var text = e.Value ? "Tự động phát: Bật" : "Tự động phát: Tắt";
        NowPlayingLabel.Text = text;
    }

    private void OnBatteryOptimizedToggled(object? sender, ToggledEventArgs e)
    {
        _batteryOptimized = e.Value;
        var text = _batteryOptimized ? "Chế độ pin: Tối ưu" : "Chế độ pin: Bình thường";
        PoiBadgeLabel.Text = text.ToUpperInvariant();
    }
}
