using System.Diagnostics;
using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Mvc;
using SharedLib.Models;

namespace AdminWeb.Controllers;

public class HomeController : Controller
{
    private readonly PoiApiClient _poiApiClient;

    public HomeController(PoiApiClient poiApiClient)
    {
        _poiApiClient = poiApiClient;
    }

    public async Task<IActionResult> Index()
    {
        var pois = await _poiApiClient.GetAllAsync();
        var orderedPois = pois.OrderByDescending(poi => poi.Priority).ThenBy(poi => poi.Name).ToList();

        var model = new AdminDashboardViewModel
        {
            TotalPois = orderedPois.Count,
            HighPriorityPois = orderedPois.Count(poi => poi.Priority >= 7),
            AverageRadius = orderedPois.Count == 0 ? 0 : orderedPois.Average(poi => poi.Radius),
            AveragePriority = orderedPois.Count == 0 ? 0 : orderedPois.Average(poi => poi.Priority),
            DistinctLanguages = orderedPois.Select(poi => poi.LanguageCode)
                .Where(language => !string.IsNullOrWhiteSpace(language))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            MediaPois = orderedPois.Count(poi => !string.IsNullOrWhiteSpace(poi.ImageUrl) || !string.IsNullOrWhiteSpace(poi.AudioUrl)),
            RecentPois = orderedPois.Take(6).ToList(),
            ModuleCards = new List<AdminModuleCardViewModel>
            {
                new()
                {
                    Title = "Bản đồ",
                    Description = "Điểm đến đang được hiển thị trên mobile app.",
                    Badge = "Live"
                },
                new()
                {
                    Title = "Hành trình",
                    Description = "Luồng nội dung hỗ trợ phần lưu lịch sử chuyến đi.",
                    Badge = "Trips"
                },
                new()
                {
                    Title = "Cài đặt",
                    Description = "Các cấu hình người dùng và trải nghiệm ứng dụng.",
                    Badge = "Prefs"
                },
                new()
                {
                    Title = "Trợ giúp",
                    Description = "Khu vực tiếp nhận phản hồi và hỗ trợ người dùng.",
                    Badge = "Support"
                }
            }
        };

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
