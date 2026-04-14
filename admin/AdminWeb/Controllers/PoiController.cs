using AdminWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedLib.Models;

namespace AdminWeb.Controllers;

[Authorize(Roles = "admin")]
public class PoiController : Controller
{
    private readonly PoiApiClient _poiApiClient;

    public PoiController(PoiApiClient poiApiClient)
    {
        _poiApiClient = poiApiClient;
    }

    public async Task<IActionResult> Index()
    {
        var pois = await _poiApiClient.GetAllAsync();
        return View(pois.OrderByDescending(poi => poi.Priority).ThenBy(poi => poi.Name).ToList());
    }

    public async Task<IActionResult> Map()
    {
        var pois = await _poiApiClient.GetAllAsync();
        return View(pois.OrderByDescending(poi => poi.Priority).ThenBy(poi => poi.Name).ToList());
    }

    public IActionResult Create()
    {
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
        var deleted = await _poiApiClient.DeleteAsync(id);
        TempData["AdminMessage"] = deleted ? "Đã xoá POI." : "Không thể xoá POI trên backend.";
        return RedirectToAction(nameof(Index));
    }
}
