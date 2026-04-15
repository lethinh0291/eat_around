using System.Globalization;
using System.Text.Json;
using MobileApp.Resources.Localization;
using MobileApp.Services;
using SharedLib.Models;
using ZXing.Net.Maui;

namespace ZesTour.Views;

public partial class QrTriggerPage : ContentPage
{
    private const string TripsHistoryKey = "zes_trip_history_v1";
    private readonly DatabaseService _databaseService;
    private readonly ApiService _apiService;
    private readonly LocationService _locationService;
    private bool _isProcessing;

    public QrTriggerPage(DatabaseService databaseService, ApiService apiService, LocationService locationService)
    {
        _databaseService = databaseService;
        _apiService = apiService;
        _locationService = locationService;

        InitializeComponent();
        ApplyLocalizedText();
        ConfigureScanner();
    }

    private void ApplyLocalizedText()
    {
        TitleLabel.Text = AppText.Get("QrTrigger_Title");
        SubtitleLabel.Text = AppText.Get("QrTrigger_Subtitle");
        OverlayHintLabel.Text = AppText.Get("QrTrigger_OverlayHint");
        StatusLabel.Text = AppText.Get("QrTrigger_Ready");
        RetryButton.Text = AppText.Get("QrTrigger_Retry");
    }

    private void ConfigureScanner()
    {
        QrCameraView.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.TwoDimensional,
            AutoRotate = true,
            Multiple = false
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        QrCameraView.IsDetecting = !_isProcessing;
    }

    protected override void OnDisappearing()
    {
        QrCameraView.IsDetecting = false;
        base.OnDisappearing();
    }

    private async void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (_isProcessing)
        {
            return;
        }

        var qrRaw = e.Results.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(qrRaw))
        {
            return;
        }

        _isProcessing = true;
        QrCameraView.IsDetecting = false;
        var shouldResumeDetecting = true;

        try
        {
            shouldResumeDetecting = await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                StatusLabel.Text = AppText.Get("QrTrigger_Processing");
                return await HandleQrValueAsync(qrRaw.Trim());
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"QR processing failed: {ex.Message}");
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                StatusLabel.Text = AppText.Get("QrTrigger_PlaybackFailed");
                await DisplayAlertAsync(
                    AppText.Get("QrTrigger_PlaybackFailedTitle"),
                    AppText.Get("QrTrigger_PlaybackFailed"),
                    AppText.Get("Common_Ok"));
            });
        }
        finally
        {
            _isProcessing = false;
            if (shouldResumeDetecting)
            {
                QrCameraView.IsDetecting = true;
            }
        }
    }

    private async Task<bool> HandleQrValueAsync(string rawValue)
    {
        if (!TryExtractPayload(rawValue, out var payload))
        {
            StatusLabel.Text = AppText.Get("QrTrigger_Unsupported");
            await DisplayAlertAsync(
                AppText.Get("QrTrigger_UnsupportedTitle"),
                AppText.Get("QrTrigger_Unsupported"),
                AppText.Get("Common_Ok"));
            return true;
        }

        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            await _apiService.TrackQrScanAsync(payload.PoiId, payload.LanguageCode, cancellationToken: CancellationToken.None);
        }

        var poi = await ResolvePoiAsync(payload.PoiId);
        if (poi is null)
        {
            var hasInternet = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
            var message = hasInternet
                ? AppText.Get("QrTrigger_PoiNotFound")
                : AppText.Get("QrTrigger_PoiNotFoundOffline");

            StatusLabel.Text = message;
            await DisplayAlertAsync(
                AppText.Get("QrTrigger_NotFoundTitle"),
                message,
                AppText.Get("Common_Ok"));
            return true;
        }

        var narrationLanguage = string.IsNullOrWhiteSpace(payload.LanguageCode)
            ? null
            : payload.LanguageCode;

        var narrated = await _locationService.NarratePoiAsync(poi, null, CancellationToken.None, narrationLanguage);
        if (!narrated)
        {
            StatusLabel.Text = AppText.Get("QrTrigger_PlaybackFailed");
            await DisplayAlertAsync(
                AppText.Get("QrTrigger_PlaybackFailedTitle"),
                AppText.Get("QrTrigger_PlaybackFailed"),
                AppText.Get("Common_Ok"));
            return true;
        }

        SaveTripSelection(poi.Name, poi.Description);
        StatusLabel.Text = AppText.Format("QrTrigger_Success", poi.Name);

        await DisplayAlertAsync(
            AppText.Get("QrTrigger_SuccessTitle"),
            AppText.Format("QrTrigger_Success", poi.Name),
            AppText.Get("Common_Ok"));

        await Navigation.PopAsync();
        return false;
    }

    private async Task<POI?> ResolvePoiAsync(int poiId)
    {
        var localPois = await _databaseService.GetPOIsAsync();
        var localPoi = localPois.FirstOrDefault(item => item.Id == poiId);
        if (localPoi is not null)
        {
            return localPoi;
        }

        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            return null;
        }

        var remotePois = await _apiService.GetPoisAsync();
        if (remotePois.Count > 0)
        {
            await _databaseService.SavePoisAsync(remotePois);
        }

        return remotePois.FirstOrDefault(item => item.Id == poiId);
    }

    private static bool TryExtractPayload(string rawValue, out QrPayload payload)
    {
        payload = default;

        if (TryParseUriPayload(rawValue, out payload))
        {
            return true;
        }

        if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericPoiId) && numericPoiId > 0)
        {
            payload = new QrPayload(numericPoiId, null);
            return true;
        }

        var separators = new[] { '|', ';', ',' };
        foreach (var separator in separators)
        {
            var parts = rawValue.Split(separator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 1)
            {
                continue;
            }

            if (int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var poiId) && poiId > 0)
            {
                var language = parts.Length > 1 ? NormalizeLanguageCode(parts[1]) : null;
                payload = new QrPayload(poiId, language);
                return true;
            }
        }

        return false;
    }

    private static bool TryParseUriPayload(string rawValue, out QrPayload payload)
    {
        payload = default;

        if (!Uri.TryCreate(rawValue, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var query = ParseQuery(uri.Query);
        if (!query.TryGetValue("poiId", out var poiIdText) && !query.TryGetValue("poi", out poiIdText))
        {
            return false;
        }

        if (!int.TryParse(poiIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var poiId) || poiId <= 0)
        {
            return false;
        }

        query.TryGetValue("lang", out var langText);
        payload = new QrPayload(poiId, NormalizeLanguageCode(langText));
        return true;
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
            if (pair.Length == 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(pair[0]);
            var value = pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : string.Empty;
            result[key] = value;
        }

        return result;
    }

    private static string? NormalizeLanguageCode(string? rawLanguage)
    {
        if (string.IsNullOrWhiteSpace(rawLanguage))
        {
            return null;
        }

        var normalized = rawLanguage.Trim().ToLowerInvariant();
        var delimiter = normalized.IndexOfAny(new[] { '-', '_' });
        if (delimiter > 0)
        {
            normalized = normalized[..delimiter];
        }

        return normalized switch
        {
            "vn" => "vi",
            _ => normalized
        };
    }

    private static void SaveTripSelection(string name, string? description)
    {
        var selectedTrip = new TripHistoryItem
        {
            Name = string.IsNullOrWhiteSpace(name) ? AppText.Get("Main_DefaultPointName") : name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? AppText.Get("Main_NoDescription") : description.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        Preferences.Default.Set(TripsHistoryKey, JsonSerializer.Serialize(selectedTrip));
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        _isProcessing = false;
        StatusLabel.Text = AppText.Get("QrTrigger_Ready");
        QrCameraView.IsDetecting = true;
        await Task.CompletedTask;
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        await Navigation.PopAsync();
    }

    private readonly record struct QrPayload(int PoiId, string? LanguageCode);

    private sealed class TripHistoryItem
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }
}
