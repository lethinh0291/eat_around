using AdminWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedLib.Models;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace AdminWeb.Controllers;

[Authorize(Roles = "seller")]
public class OwnerPortalController : Controller
{
    private readonly AdminManagementApiClient _adminManagementApiClient;
    private readonly PoiApiClient _poiApiClient;

    public OwnerPortalController(AdminManagementApiClient adminManagementApiClient, PoiApiClient poiApiClient)
    {
        _adminManagementApiClient = adminManagementApiClient;
        _poiApiClient = poiApiClient;
    }

    public async Task<IActionResult> Index()
    {
        var ownerName = User.Identity?.Name?.Trim() ?? string.Empty;
        var ownerUsername = User.FindFirstValue(ClaimTypes.GivenName)?.Trim() ?? string.Empty;

        var pendingStores = await _adminManagementApiClient.GetStoreRegistrationsAsync();
        var restaurants = await _poiApiClient.GetAllAsync();

        var myPendingStores = pendingStores
            .Where(item => IsSameOwner(item.OwnerName, ownerName, ownerUsername))
            .OrderByDescending(item => item.SubmittedAtUtc)
            .ToList();

        var myRestaurants = restaurants
            .Where(item => IsSameOwner(ExtractOwnerNameFromPoiDescription(item.Description), ownerName, ownerUsername))
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.Name)
            .ToList();

        ViewData["OwnerName"] = string.IsNullOrWhiteSpace(ownerName) ? ownerUsername : ownerName;
        ViewData["PendingCount"] = myPendingStores.Count;
        ViewData["LiveCount"] = myRestaurants.Count;
        ViewData["LatestSubmit"] = myPendingStores.FirstOrDefault()?.SubmittedAtUtc;

        var model = new OwnerPortalViewModel
        {
            PendingStores = myPendingStores,
            LiveRestaurants = myRestaurants,
            EditableLiveRestaurants = myRestaurants
                .Select(item => new EditableLiveRestaurantItem
                {
                    Poi = item,
                    NarrationDescription = ExtractNarrationDescription(item.Description)
                })
                .ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateNarrationDescription(int id, string? narrationDescription)
    {
        var ownerName = User.Identity?.Name?.Trim() ?? string.Empty;
        var ownerUsername = User.FindFirstValue(ClaimTypes.GivenName)?.Trim() ?? string.Empty;
        var normalizedNarration = narrationDescription?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedNarration))
        {
            TempData["AdminError"] = "Mô tả thuyết minh không được để trống.";
            return RedirectToAction(nameof(Index));
        }

        var poi = await _poiApiClient.GetByIdAsync(id);
        if (poi is null)
        {
            TempData["AdminError"] = "Không tìm thấy quán cần cập nhật.";
            return RedirectToAction(nameof(Index));
        }

        if (!IsSameOwner(ExtractOwnerNameFromPoiDescription(poi.Description), ownerName, ownerUsername))
        {
            return Forbid();
        }

        poi.Description = UpsertNarrationDescription(poi.Description, normalizedNarration);
        var updated = await _poiApiClient.UpdateAsync(poi);

        TempData["AdminMessage"] = updated
            ? $"Đã cập nhật mô tả thuyết minh cho quán {poi.Name}."
            : "Không thể cập nhật mô tả thuyết minh lúc này.";

        return RedirectToAction(nameof(Index));
    }

    private static bool IsSameOwner(string? candidate, string ownerName, string ownerUsername)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var normalizedCandidate = candidate.Trim();
        return string.Equals(normalizedCandidate, ownerName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalizedCandidate, ownerUsername, StringComparison.OrdinalIgnoreCase);
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

    private static string ExtractNarrationDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        var match = Regex.Match(description, @"Mô tả:\s*([^|]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : description.Trim();
    }

    private static string UpsertNarrationDescription(string? sourceDescription, string narrationDescription)
    {
        var normalizedNarration = narrationDescription.Trim();
        if (string.IsNullOrWhiteSpace(sourceDescription))
        {
            return $"Mô tả: {normalizedNarration}";
        }

        if (Regex.IsMatch(sourceDescription, @"Mô tả:\s*([^|]+)", RegexOptions.IgnoreCase))
        {
            return Regex.Replace(
                sourceDescription,
                @"Mô tả:\s*([^|]+)",
                _ => $"Mô tả: {normalizedNarration}",
                RegexOptions.IgnoreCase);
        }

        return $"{sourceDescription.Trim()} | Mô tả: {normalizedNarration}";
    }

    public sealed class OwnerPortalViewModel
    {
        public List<AdminManagementApiClient.AdminStoreRegistrationDto> PendingStores { get; set; } = new();
        public List<POI> LiveRestaurants { get; set; } = new();
        public List<EditableLiveRestaurantItem> EditableLiveRestaurants { get; set; } = new();
    }

    public sealed class EditableLiveRestaurantItem
    {
        public POI Poi { get; set; } = new();
        public string NarrationDescription { get; set; } = string.Empty;
    }
}
