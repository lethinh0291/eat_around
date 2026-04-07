using SharedLib.Models;

namespace MobileApp.Services;

public class LocationService
{
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

    public async Task CheckAndNarrate(Location userLoc, List<POI> allPois)
    {
        foreach (var poi in allPois)
        {
            double distance = CalculateDistance(userLoc.Latitude, userLoc.Longitude, poi.Latitude, poi.Longitude);

            // Nếu lọt vào bán kính thiết lập (Radius)
            if (distance <= poi.Radius)
            {
                await TextToSpeech.Default.SpeakAsync(poi.Description ?? string.Empty);
                // Sau khi nói xong, nên có cơ chế "Cooldown" để không nói đi nói lại 1 điểm
                break;
            }
        }
    }
}