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
            LiveRestaurants = myRestaurants
        };

        return View(model);
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

    public sealed class OwnerPortalViewModel
    {
        public List<AdminManagementApiClient.AdminStoreRegistrationDto> PendingStores { get; set; } = new();
        public List<POI> LiveRestaurants { get; set; } = new();
    }
}
