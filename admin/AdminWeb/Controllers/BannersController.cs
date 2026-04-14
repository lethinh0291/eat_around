using AdminWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminWeb.Controllers;

[Authorize(Roles = "admin")]
public class BannersController : Controller
{
    private readonly AdminManagementApiClient _adminManagementApiClient;

    public BannersController(AdminManagementApiClient adminManagementApiClient)
    {
        _adminManagementApiClient = adminManagementApiClient;
    }

    public async Task<IActionResult> Index()
    {
        var banners = await _adminManagementApiClient.GetAdBannersAsync();
        return View(banners.OrderBy(item => item.SortOrder).ThenByDescending(item => item.CreatedAtUtc).ToList());
    }

    public IActionResult Create()
    {
        return View(new AdminManagementApiClient.AdBannerUpsertDto
        {
            IsActive = true,
            SortOrder = 0
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminManagementApiClient.AdBannerUpsertDto request, IFormFile? imageFile)
    {
        if (imageFile is null || imageFile.Length == 0)
        {
            ModelState.AddModelError(nameof(request.ImageUrl), "Vui lòng chọn ảnh banner từ máy.");
        }

        if (!ModelState.IsValid)
        {
            return View(request);
        }

        await using (var stream = imageFile!.OpenReadStream())
        {
            var upload = await _adminManagementApiClient.UploadAdBannerImageAsync(stream, imageFile.FileName, imageFile.ContentType);
            if (!upload.Success || string.IsNullOrWhiteSpace(upload.ImageUrl))
            {
                ModelState.AddModelError(string.Empty, upload.Message);
                return View(request);
            }

            request.ImageUrl = upload.ImageUrl;
        }

        var result = await _adminManagementApiClient.CreateAdBannerAsync(request);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(request);
        }

        TempData["AdminMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var banner = await _adminManagementApiClient.GetAdBannerByIdAsync(id);
        if (banner is null)
        {
            return NotFound();
        }

        return View(new AdminManagementApiClient.AdBannerUpsertDto
        {
            ImageUrl = banner.ImageUrl,
            IsActive = banner.IsActive,
            SortOrder = banner.SortOrder
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminManagementApiClient.AdBannerUpsertDto request, IFormFile? imageFile, string? currentImageUrl)
    {
        if (imageFile is not null && imageFile.Length > 0)
        {
            await using var stream = imageFile.OpenReadStream();
            var upload = await _adminManagementApiClient.UploadAdBannerImageAsync(stream, imageFile.FileName, imageFile.ContentType);
            if (!upload.Success || string.IsNullOrWhiteSpace(upload.ImageUrl))
            {
                ModelState.AddModelError(string.Empty, upload.Message);
                return View(request);
            }

            request.ImageUrl = upload.ImageUrl;
        }
        else
        {
            request.ImageUrl = currentImageUrl?.Trim() ?? string.Empty;
            if (Uri.TryCreate(request.ImageUrl, UriKind.Absolute, out var absoluteUri))
            {
                request.ImageUrl = absoluteUri.AbsolutePath;
            }
        }

        if (string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            ModelState.AddModelError(nameof(request.ImageUrl), "Vui lòng chọn ảnh banner từ máy.");
        }

        if (!ModelState.IsValid)
        {
            return View(request);
        }

        var result = await _adminManagementApiClient.UpdateAdBannerAsync(id, request);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(request);
        }

        TempData["AdminMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _adminManagementApiClient.DeleteAdBannerAsync(id);
        TempData[result.Success ? "AdminMessage" : "AdminError"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
