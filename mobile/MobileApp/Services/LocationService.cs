using SharedLib.Models;

namespace MobileApp.Services;

public class LocationService
{
    private readonly Dictionary<int, DateTime> _lastNarratedAtByPoi = new();
    private readonly SemaphoreSlim _speakLock = new(1, 1);

    public TimeSpan NarrationCooldown { get; set; } = TimeSpan.FromMinutes(2);

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

            if (distance > poi.Radius)
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

    public async Task<bool> NarratePoiAsync(POI poi, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(poi.Description))
        {
            return false;
        }

        var nowUtc = DateTime.UtcNow;
        if (!CanNarrate(poi, nowUtc))
        {
            return false;
        }

        await _speakLock.WaitAsync(cancellationToken);
        try
        {
            // Kiểm tra lại sau khi đợi lock để tránh phát lặp trong lúc chờ hàng đợi.
            nowUtc = DateTime.UtcNow;
            if (!CanNarrate(poi, nowUtc))
            {
                return false;
            }

            var options = new SpeechOptions();
            if (!string.IsNullOrWhiteSpace(poi.LanguageCode))
            {
                var locales = await TextToSpeech.Default.GetLocalesAsync();
                var locale = locales.FirstOrDefault(candidate =>
                    candidate.Language.StartsWith(poi.LanguageCode, StringComparison.OrdinalIgnoreCase));

                if (locale is not null)
                {
                    options.Locale = locale;
                }
            }

            await TextToSpeech.Default.SpeakAsync(poi.Description, options, cancellationToken);
            _lastNarratedAtByPoi[poi.Id] = nowUtc;
            return true;
        }
        finally
        {
            _speakLock.Release();
        }
    }
}