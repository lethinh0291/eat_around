using SharedLib.Models;
using System.Globalization;
using Microsoft.Maui.Storage;
using System.Text.RegularExpressions;

namespace MobileApp.Services;

public class LocationService
{
    private const string SettingsKey = "zes_settings_v1";
    private readonly ApiService _apiService;
    private readonly AudioPlaybackService _audioPlaybackService;
    private readonly TranslationService _translationService;
    private readonly Dictionary<int, DateTime> _lastNarratedAtByPoi = new();
    private readonly Dictionary<string, DateTime> _lastNarratedAtByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _lastAutoNarrationAttemptAtByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _speakLock = new(1, 1);
    private string? _activeAutoPoiKey;
    private string? _pendingAutoPoiKey;
    private DateTime _pendingAutoPoiSinceUtc;

    public TimeSpan NarrationCooldown { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan AutoNarrationDebounce { get; set; } = TimeSpan.FromSeconds(3);
    public TimeSpan AutoNarrationRetryDelay { get; set; } = TimeSpan.FromSeconds(10);
    public double PoiRadiusScale { get; set; } = 1.0;

    public LocationService(ApiService apiService, AudioPlaybackService audioPlaybackService, TranslationService translationService)
    {
        _apiService = apiService;
        _audioPlaybackService = audioPlaybackService;
        _translationService = translationService;
    }

    // Hàm tính khoảng cách giữa 2 tọa độ (Công thức Haversine)
    public double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        double r = 6371000; // Bán kính Trái Đất (mét)
        double dLat = (lat2 - lat1) * Math.PI / 180;
        double dLon = (lon2 - lon1) * Math.PI / 180;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return r * c;
    }

    public POI? FindBestPoiInRange(Location userLoc, IEnumerable<POI> allPois)
    {
        POI? selected = null;
        var bestDistance = double.MaxValue;

        foreach (var poi in allPois)
        {
            var distance = CalculateDistance(userLoc.Latitude, userLoc.Longitude, poi.Latitude, poi.Longitude);

            var effectiveRadius = Math.Max(1, poi.Radius * PoiRadiusScale);
            if (distance > effectiveRadius)
            {
                continue;
            }

            if (selected is null)
            {
                selected = poi;
                bestDistance = distance;
                continue;
            }

            // Ưu tiên POI có priority cao hơn, nếu bằng nhau thì chọn POI gần hơn.
            if (poi.Priority > selected.Priority ||
                (poi.Priority == selected.Priority && distance < bestDistance))
            {
                selected = poi;
                bestDistance = distance;
            }
        }

        return selected;
    }

    public bool CanNarrate(POI poi, DateTime nowUtc)
    {
        if (!_lastNarratedAtByPoi.TryGetValue(poi.Id, out var lastPlayedAtUtc))
        {
            return true;
        }

        return nowUtc - lastPlayedAtUtc >= NarrationCooldown;
    }

    public bool CanNarrate(string key, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (!_lastNarratedAtByKey.TryGetValue(key, out var lastPlayedAtUtc))
        {
            return true;
        }

        return nowUtc - lastPlayedAtUtc >= NarrationCooldown;
    }

    public async Task<POI?> TryNarrateAutoPoiAsync(
        Location userLocation,
        IReadOnlyList<POI> allPois,
        CancellationToken cancellationToken = default,
        string? preferredLanguageOverride = null)
    {
        if (allPois.Count == 0)
        {
            ResetAutoNarrationState();
            return null;
        }

        var candidate = FindBestPoiInRange(userLocation, allPois);
        if (candidate is null)
        {
            ResetAutoNarrationState();
            return null;
        }

        var narrationKey = GetAutoNarrationKey(candidate.Id);
        var nowUtc = DateTime.UtcNow;

        if (!string.Equals(_activeAutoPoiKey, narrationKey, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(_pendingAutoPoiKey, narrationKey, StringComparison.OrdinalIgnoreCase))
        {
            _pendingAutoPoiKey = narrationKey;
            _pendingAutoPoiSinceUtc = nowUtc;
            return null;
        }

        if (!string.Equals(_pendingAutoPoiKey, narrationKey, StringComparison.OrdinalIgnoreCase))
        {
            _pendingAutoPoiKey = narrationKey;
            _pendingAutoPoiSinceUtc = nowUtc;
            return null;
        }

        if (nowUtc - _pendingAutoPoiSinceUtc < AutoNarrationDebounce)
        {
            return null;
        }

        if (string.Equals(_activeAutoPoiKey, narrationKey, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (_lastAutoNarrationAttemptAtByKey.TryGetValue(narrationKey, out var lastAttemptAtUtc) &&
            nowUtc - lastAttemptAtUtc < AutoNarrationRetryDelay)
        {
            return null;
        }

        if (!CanNarrate(narrationKey, nowUtc))
        {
            _activeAutoPoiKey = narrationKey;
            return null;
        }

        _lastAutoNarrationAttemptAtByKey[narrationKey] = nowUtc;

        var narrated = await NarratePoiAsync(candidate, userLocation, cancellationToken, preferredLanguageOverride);
        if (!narrated)
        {
            return null;
        }

        _activeAutoPoiKey = narrationKey;
        _pendingAutoPoiKey = narrationKey;
        _pendingAutoPoiSinceUtc = nowUtc;
        return candidate;
    }

    public async Task<bool> NarratePoiAsync(
        POI poi,
        Location? userLocation = null,
        CancellationToken cancellationToken = default,
        string? preferredLanguageOverride = null)
    {
        var narrationKey = $"poi:{poi.Id}";
        var narrationSource = ExtractNarrationText(poi.Description);
        if (string.IsNullOrWhiteSpace(narrationSource) || !CanNarrate(narrationKey, DateTime.UtcNow))
        {
            return false;
        }

        await _speakLock.WaitAsync(cancellationToken);
        try
        {
            var nowUtc = DateTime.UtcNow;
            if (!CanNarrate(narrationKey, nowUtc))
            {
                return false;
            }

            var sourceText = narrationSource;
            var sourceLanguage = NormalizeLanguageCode(poi.LanguageCode);
            var preferredLanguage = ResolvePreferredLanguageCode(preferredLanguageOverride);
            var selectedLanguage = sourceLanguage;
            var selectedText = sourceText;
            var selectedAudioUrl = NormalizeAudioUrl(poi.AudioUrl);
            var hasInternet = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
            var narrationType = "tts";

            if (!string.Equals(preferredLanguage, sourceLanguage, StringComparison.OrdinalIgnoreCase))
            {
                var approvedTranslation = await _apiService.GetApprovedPoiTranslationAsync(poi.Id, preferredLanguage, cancellationToken);

                if (approvedTranslation is not null)
                {
                    if (!string.IsNullOrWhiteSpace(approvedTranslation.ContentText))
                    {
                        selectedText = approvedTranslation.ContentText.Trim();
                    }

                    selectedLanguage = NormalizeLanguageCode(approvedTranslation.LanguageCode);
                    selectedAudioUrl = NormalizeAudioUrl(approvedTranslation.AudioUrl) ?? selectedAudioUrl;
                }
                else if (hasInternet)
                {
                    var dynamicTranslation = await _translationService.TranslateAsync(
                        sourceText,
                        preferredLanguage,
                        sourceLanguage,
                        cancellationToken);

                    if (!string.IsNullOrWhiteSpace(dynamicTranslation))
                    {
                        selectedText = dynamicTranslation.Trim();
                        selectedLanguage = preferredLanguage;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(selectedAudioUrl))
            {
                narrationType = "audio";
                var audioWatch = System.Diagnostics.Stopwatch.StartNew();
                var played = await _audioPlaybackService.TryPlayFromUrlAsync(selectedAudioUrl, cancellationToken);
                if (played)
                {
                    audioWatch.Stop();
                    MarkNarrated(poi.Id, narrationKey, nowUtc);
                    await LogListenAsync(poi.Id, narrationType, selectedLanguage, audioWatch.Elapsed.TotalSeconds, userLocation, cancellationToken);
                    return true;
                }
            }

            narrationType = "tts";
            var ttsWatch = System.Diagnostics.Stopwatch.StartNew();
            var spoken = await SpeakTextInternalAsync(selectedText, selectedLanguage, cancellationToken);
            if (!spoken)
            {
                return false;
            }

            ttsWatch.Stop();
            MarkNarrated(poi.Id, narrationKey, nowUtc);
            await LogListenAsync(poi.Id, narrationType, selectedLanguage, ttsWatch.Elapsed.TotalSeconds, userLocation, cancellationToken);
            return true;
        }
        finally
        {
            _speakLock.Release();
        }
    }

    public async Task<bool> NarrateTextAsync(
        string narrationKey,
        string? text,
        string languageCode = "vi",
        CancellationToken cancellationToken = default,
        Action<DateTime>? onNarratedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(narrationKey))
        {
            return false;
        }

        var nowUtc = DateTime.UtcNow;
        if (!CanNarrate(narrationKey, nowUtc))
        {
            return false;
        }

        await _speakLock.WaitAsync(cancellationToken);
        try
        {
            // Kiểm tra lại sau khi đợi lock để tránh phát lặp trong lúc chờ hàng đợi.
            nowUtc = DateTime.UtcNow;
            if (!CanNarrate(narrationKey, nowUtc))
            {
                return false;
            }

            var spoken = await SpeakTextInternalAsync(text, languageCode, cancellationToken);
            if (!spoken)
            {
                return false;
            }

            _lastNarratedAtByKey[narrationKey] = nowUtc;
            onNarratedAtUtc?.Invoke(nowUtc);
            return true;
        }
        finally
        {
            _speakLock.Release();
        }
    }

    private static async Task<bool> SpeakTextInternalAsync(string? text, string languageCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var options = new SpeechOptions();
        var normalizedLanguage = NormalizeLanguageCode(languageCode);
        var preferredTtsVoiceCode = GetPreferredTtsVoiceCode();

        var locales = await TextToSpeech.Default.GetLocalesAsync();
        if (!string.IsNullOrWhiteSpace(preferredTtsVoiceCode) &&
            !string.Equals(preferredTtsVoiceCode, "auto", StringComparison.OrdinalIgnoreCase))
        {
            var preferredLocale = locales.FirstOrDefault(candidate =>
                candidate.Language.Equals(preferredTtsVoiceCode, StringComparison.OrdinalIgnoreCase));

            if (preferredLocale is not null)
            {
                options.Locale = preferredLocale;
            }
        }

        if (options.Locale is null && !string.IsNullOrWhiteSpace(normalizedLanguage))
        {
            var locale = locales.FirstOrDefault(candidate =>
                candidate.Language.StartsWith(normalizedLanguage, StringComparison.OrdinalIgnoreCase));

            if (locale is not null)
            {
                options.Locale = locale;
            }
        }

        await TextToSpeech.Default.SpeakAsync(text, options, cancellationToken);
        return true;
    }

    private static string GetPreferredLanguageCode()
    {
        var json = Preferences.Default.Get(SettingsKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<SettingData>(json);
                var selected = data?.NarrationLanguageCode;
                if (!string.IsNullOrWhiteSpace(selected) &&
                    !string.Equals(selected, "auto", StringComparison.OrdinalIgnoreCase))
                {
                    return NormalizeLanguageCode(selected);
                }
            }
            catch
            {
                // Fall back to the device language.
            }
        }

        var code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return NormalizeLanguageCode(code);
    }

    private static string GetPreferredTtsVoiceCode()
    {
        var json = Preferences.Default.Get(SettingsKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<SettingData>(json);
                var selected = data?.TtsVoiceCode;
                if (!string.IsNullOrWhiteSpace(selected))
                {
                    return selected.Trim();
                }
            }
            catch
            {
                // Fall back to automatic locale selection.
            }
        }

        return "auto";
    }

    private static string ResolvePreferredLanguageCode(string? preferredLanguageOverride)
    {
        if (!string.IsNullOrWhiteSpace(preferredLanguageOverride))
        {
            return NormalizeLanguageCode(preferredLanguageOverride);
        }

        return GetPreferredLanguageCode();
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return "vi";
        }

        var normalized = languageCode.Trim().ToLowerInvariant();
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

    private static string? NormalizeAudioUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return url.Trim();
    }

    private static string ExtractNarrationText(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        var match = Regex.Match(description, @"Mô tả:\s*([^|]+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        return description.Trim();
    }

    private void ResetAutoNarrationState()
    {
        _activeAutoPoiKey = null;
        _pendingAutoPoiKey = null;
        _pendingAutoPoiSinceUtc = default;
    }

    private static string GetAutoNarrationKey(int poiId)
    {
        return $"poi:{poiId}";
    }

    private void MarkNarrated(int poiId, string narrationKey, DateTime narratedAtUtc)
    {
        _lastNarratedAtByPoi[poiId] = narratedAtUtc;
        _lastNarratedAtByKey[narrationKey] = narratedAtUtc;
    }

    private async Task LogListenAsync(
        int poiId,
        string contentType,
        string languageCode,
        double durationSeconds,
        Location? userLocation,
        CancellationToken cancellationToken)
    {
        if (userLocation is null)
        {
            return;
        }

        await _apiService.RecordListenLogAsync(
            poiId,
            languageCode,
            contentType,
            durationSeconds,
            userLocation.Latitude,
            userLocation.Longitude,
            DateTime.UtcNow,
            cancellationToken);
    }

    private sealed class SettingData
    {
        public string NarrationLanguageCode { get; set; } = "auto";
        public string TtsVoiceCode { get; set; } = "auto";
    }
}