using AdminWeb.Services;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedLib.Models;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace AdminWeb.Controllers;

[Authorize(Roles = "admin,seller")]
public class PoiController : Controller
{
    private readonly PoiApiClient _poiApiClient;

    public PoiController(PoiApiClient poiApiClient)
    {
        _poiApiClient = poiApiClient;
    }

    public async Task<IActionResult> Index(string? search, string? status)
    {
        var normalizedSearch = (search ?? string.Empty).Trim();
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim().ToLowerInvariant();

        var pois = await _poiApiClient.GetAllAsync();
        var visiblePois = FilterPoisByRole(pois)
            .Where(poi => string.IsNullOrWhiteSpace(normalizedSearch) ||
                          poi.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(poi => poi.Priority)
            .ThenBy(poi => poi.Name)
            .ToList();

        var items = new List<PoiListItemViewModel>(visiblePois.Count);
        foreach (var poi in visiblePois)
        {
            var qrTriggers = await _poiApiClient.GetQrTriggersForPoiAsync(poi.Id);
            var latestQr = qrTriggers
                .OrderByDescending(item => item.CreatedAtUtc)
                .FirstOrDefault();

            var statusCode = latestQr is null ? "no-qr" : "has-qr";
            var statusLabel = latestQr is null ? "Chua co QR" : "Da tao QR";

            items.Add(new PoiListItemViewModel
            {
                Poi = poi,
                OwnerName = ExtractOwnerNameFromPoiDescription(poi.Description),
                StatusCode = statusCode,
                StatusLabel = statusLabel,
                CreatedAtUtc = latestQr?.CreatedAtUtc,
                LatestQrId = latestQr?.Id
            });
        }

        if (!string.Equals(normalizedStatus, "all", StringComparison.OrdinalIgnoreCase))
        {
            items = items
                .Where(item => string.Equals(item.StatusCode, normalizedStatus, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var ownerDisplayName = User.Identity?.Name?.Trim();
        if (string.IsNullOrWhiteSpace(ownerDisplayName))
        {
            ownerDisplayName = User.FindFirstValue(ClaimTypes.GivenName)?.Trim() ?? string.Empty;
        }

        return View(new PoiManagementViewModel
        {
            Items = items,
            Search = normalizedSearch,
            Status = normalizedStatus,
            IsAdmin = User.IsInRole("admin"),
            IsSeller = User.IsInRole("seller"),
            CurrentOwnerDisplayName = ownerDisplayName ?? string.Empty
        });
    }

    public async Task<IActionResult> Map()
    {
        var pois = await _poiApiClient.GetAllAsync();
        var visiblePois = FilterPoisByRole(pois)
            .OrderByDescending(poi => poi.Priority)
            .ThenBy(poi => poi.Name)
            .ToList();

        return View(visiblePois);
    }

    public IActionResult Create()
    {
        if (!User.IsInRole("admin"))
        {
            return Forbid();
        }

        return View(new POI
        {
            Radius = 100,
            Priority = 1,
            LanguageCode = "vi"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(POI poi)
    {
        if (!User.IsInRole("admin"))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(poi);
        }

        var created = await _poiApiClient.CreateAsync(poi);
        if (created)
        {
            TempData["AdminMessage"] = $"Đã tạo POI {poi.Name}.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError(string.Empty, "Không thể tạo POI trên backend.");
        return View(poi);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var poi = await _poiApiClient.GetByIdAsync(id);
        if (poi is null)
        {
            return NotFound();
        }

        if (User.IsInRole("seller") && !IsCurrentSellerAssignedToPoi(poi))
        {
            return Forbid();
        }

        return View(poi);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, POI poi)
    {
        if (id != poi.Id)
        {
            return BadRequest();
        }

        if (User.IsInRole("seller"))
        {
            var existingPoi = await _poiApiClient.GetByIdAsync(id);
            if (existingPoi is null || !IsCurrentSellerAssignedToPoi(existingPoi))
            {
                return Forbid();
            }
        }

        if (!ModelState.IsValid)
        {
            return View(poi);
        }

        var updated = await _poiApiClient.UpdateAsync(poi);
        if (updated)
        {
            TempData["AdminMessage"] = $"Đã cập nhật POI {poi.Name}.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError(string.Empty, "Không thể cập nhật POI trên backend.");
        return View(poi);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!User.IsInRole("admin"))
        {
            return Forbid();
        }

        var deleted = await _poiApiClient.DeleteAsync(id);
        TempData["AdminMessage"] = deleted ? "Đã xoá POI." : "Không thể xoá POI trên backend.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateQr(int id)
    {
        var poi = await _poiApiClient.GetByIdAsync(id);
        if (poi is null)
        {
            return NotFound();
        }

        if (User.IsInRole("seller") && !IsCurrentSellerAssignedToPoi(poi))
        {
            return Forbid();
        }

        var languageCode = string.IsNullOrWhiteSpace(poi.LanguageCode) ? "vi" : poi.LanguageCode;
        var result = await _poiApiClient.GenerateQrAsync(poi.Id, languageCode);
        TempData["AdminMessage"] = result.Success ? $"Da tao QR cho {poi.Name}." : result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> DownloadQr(int id, int? qrId)
    {
        var poi = await _poiApiClient.GetByIdAsync(id);
        if (poi is null)
        {
            return NotFound();
        }

        if (User.IsInRole("seller") && !IsCurrentSellerAssignedToPoi(poi))
        {
            return Forbid();
        }

        var resolvedQrId = qrId;
        if (!resolvedQrId.HasValue)
        {
            var qrTriggers = await _poiApiClient.GetQrTriggersForPoiAsync(id);
            resolvedQrId = qrTriggers
                .OrderByDescending(item => item.CreatedAtUtc)
                .Select(item => (int?)item.Id)
                .FirstOrDefault();
        }

        if (!resolvedQrId.HasValue)
        {
            TempData["AdminMessage"] = "POI nay chua co QR de tai.";
            return RedirectToAction(nameof(Index));
        }

        var qrDetail = await _poiApiClient.GetQrTriggerByIdAsync(resolvedQrId.Value);
        if (qrDetail is null || string.IsNullOrWhiteSpace(qrDetail.QrImageBase64))
        {
            TempData["AdminMessage"] = "Khong the tai QR vao thoi diem nay.";
            return RedirectToAction(nameof(Index));
        }

        var base64 = qrDetail.QrImageBase64;
        var commaIndex = base64.IndexOf(',');
        if (commaIndex >= 0)
        {
            base64 = base64[(commaIndex + 1)..];
        }

        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            TempData["AdminMessage"] = "Du lieu QR khong hop le de tai.";
            return RedirectToAction(nameof(Index));
        }

        var safePoiName = string.Join("_", (poi.Name ?? "poi")
            .Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
            .Trim();
        if (string.IsNullOrWhiteSpace(safePoiName))
        {
            safePoiName = "poi";
        }

        return File(imageBytes, "image/png", $"qr-poi-{poi.Id}-{safePoiName}.png");
    }

    private IEnumerable<POI> FilterPoisByRole(IEnumerable<POI> source)
    {
        if (User.IsInRole("admin"))
        {
            return source;
        }

        if (!User.IsInRole("seller"))
        {
            return Enumerable.Empty<POI>();
        }

        return source.Where(IsCurrentSellerAssignedToPoi);
    }

    private bool IsCurrentSellerAssignedToPoi(POI poi)
    {
        var ownerName = User.Identity?.Name?.Trim() ?? string.Empty;
        var ownerUsername = User.FindFirstValue(ClaimTypes.GivenName)?.Trim() ?? string.Empty;
        var candidate = ExtractOwnerNameFromPoiDescription(poi.Description);

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        return string.Equals(candidate, ownerName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(candidate, ownerUsername, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractOwnerNameFromPoiDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return "Chua gan";
        }

        var match = Regex.Match(description, @"Liên hệ:\s*(.*?)\s*-", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return "Chua gan";
        }

        var owner = match.Groups[1].Value.Trim();
        return string.IsNullOrWhiteSpace(owner) ? "Chua gan" : owner;
    }
}
