using AdminWeb.Services;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedLib.Models;
using System.Text.RegularExpressions;

namespace AdminWeb.Controllers;

[Authorize]
public class AdminManagementController : Controller
{
    private readonly AdminManagementApiClient _adminManagementApiClient;
    private readonly PoiApiClient _poiApiClient;

    public AdminManagementController(AdminManagementApiClient adminManagementApiClient, PoiApiClient poiApiClient)
    {
        _adminManagementApiClient = adminManagementApiClient;
        _poiApiClient = poiApiClient;
    }

    public async Task<IActionResult> Users()
    {
        var users = await _adminManagementApiClient.GetUsersAsync();
        var pendingStores = await _adminManagementApiClient.GetStoreRegistrationsAsync();
        var restaurants = await _poiApiClient.GetAllAsync();

        if (!string.IsNullOrWhiteSpace(_adminManagementApiClient.LastUsersError))
        {
            TempData["AdminError"] = _adminManagementApiClient.LastUsersError;
        }

        var rows = users
            .OrderBy(user => user.Id)
            .Select(user => new AdminUsersViewModel.UserRow
            {
                User = user,
                OwnedStores = BuildOwnedStores(user, pendingStores, restaurants)
            })
            .ToList();

        return View(rows);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(string name, string username, string email, string password, string role)
    {
        var request = new AdminManagementApiClient.AdminUserUpsertDto
        {
            Name = name,
            Username = username,
            Email = email,
            Password = password,
            Role = role
        };

        var result = await _adminManagementApiClient.CreateUserAsync(request);
        TempData[result.Success ? "AdminMessage" : "AdminError"] = result.Message;
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUser(int userId, string name, string username, string email, string password, string role)
    {
        var request = new AdminManagementApiClient.AdminUserUpsertDto
        {
            Name = name,
            Username = username,
            Email = email,
            Password = password,
            Role = role
        };

        var result = await _adminManagementApiClient.UpdateUserAsync(userId, request);
        TempData[result.Success ? "AdminMessage" : "AdminError"] = result.Message;
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUserRole(int userId, string role)
    {
        var normalizedRole = (role ?? string.Empty).Trim().ToLowerInvariant();
        var result = await _adminManagementApiClient.UpdateUserRoleAsync(userId, normalizedRole);
        TempData[result.Success ? "AdminMessage" : "AdminError"] = result.Message;
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(int userId)
    {
        var result = await _adminManagementApiClient.DeleteUserAsync(userId);
        TempData[result.Success ? "AdminMessage" : "AdminError"] = result.Message;
        return RedirectToAction(nameof(Users));
    }

    public async Task<IActionResult> Stores()
    {
        var stores = await _adminManagementApiClient.GetStoreRegistrationsAsync();
        if (!string.IsNullOrWhiteSpace(_adminManagementApiClient.LastStoreRegistrationsError))
        {
            TempData["AdminError"] = _adminManagementApiClient.LastStoreRegistrationsError;
        }

        var restaurants = await _poiApiClient.GetAllAsync();

        var model = new AdminStoresViewModel
        {
            PendingRegistrations = stores.OrderByDescending(item => item.SubmittedAtUtc).ToList(),
            Restaurants = restaurants.OrderByDescending(item => item.Priority).ThenBy(item => item.Name).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRestaurant(POI poi)
    {
        if (poi.Id <= 0)
        {
            TempData["AdminError"] = "Dữ liệu quán ăn không hợp lệ.";
            return RedirectToAction(nameof(Stores));
        }

        var updated = await _poiApiClient.UpdateAsync(poi);
        TempData[updated ? "AdminMessage" : "AdminError"] = updated
            ? $"Đã cập nhật quán {poi.Name}."
            : "Không thể cập nhật quán ăn trên backend.";
        return RedirectToAction(nameof(Stores));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRestaurant(int id)
    {
        var deleted = await _poiApiClient.DeleteAsync(id);
        TempData[deleted ? "AdminMessage" : "AdminError"] = deleted
            ? "Đã xóa quán ăn khỏi bản đồ."
            : "Không thể xóa quán ăn trên backend.";
        return RedirectToAction(nameof(Stores));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteStore(int id)
    {
        var result = await _adminManagementApiClient.DeleteStoreRegistrationAsync(id);
        TempData[result.Success ? "AdminMessage" : "AdminError"] = result.Message;
        return RedirectToAction(nameof(Stores));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStoreRegistration(
        int id,
        string storeName,
        string ownerName,
        string? imageUrl,
        string phone,
        string address,
        string? category,
        string? description)
    {
        var request = new AdminManagementApiClient.AdminStoreRegistrationUpsertDto
        {
            StoreName = storeName,
            OwnerName = ownerName,
            ImageUrl = imageUrl,
            Phone = phone,
            Address = address,
            Category = category ?? string.Empty,
            Description = description ?? string.Empty
        };

        var result = await _adminManagementApiClient.UpdateStoreRegistrationAsync(id, request);
        TempData[result.Success ? "AdminMessage" : "AdminError"] = result.Message;
        return RedirectToAction(nameof(Stores));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveStore(int id, double latitude, double longitude)
    {
        if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
        {
            TempData["AdminError"] = "Tọa độ không hợp lệ. Vui lòng chọn lại trên map.";
            return RedirectToAction(nameof(Stores));
        }

        var stores = await _adminManagementApiClient.GetStoreRegistrationsAsync();
        var store = stores.FirstOrDefault(item => item.Id == id);
        if (store is null)
        {
            TempData["AdminError"] = "Không tìm thấy đăng ký quán ăn cần duyệt.";
            return RedirectToAction(nameof(Stores));
        }

        var description = BuildPoiDescription(store);
        var poi = new POI
        {
            Name = store.StoreName,
            Description = description,
            ImageUrl = store.ImageUrl,
            Latitude = latitude,
            Longitude = longitude,
            Radius = 250,
            Priority = 6,
            LanguageCode = "vi"
        };

        var created = await _poiApiClient.CreateAsync(poi);
        if (!created)
        {
            TempData["AdminError"] = "Không thể tạo POI cho quán ăn đã duyệt.";
            return RedirectToAction(nameof(Stores));
        }

        var deleted = await _adminManagementApiClient.DeleteStoreRegistrationAsync(id);
        if (!deleted.Success)
        {
            TempData["AdminError"] = "Đã tạo POI nhưng chưa xóa được đăng ký khỏi danh sách chờ.";
            return RedirectToAction(nameof(Stores));
        }

        TempData["AdminMessage"] = $"Đã duyệt quán {store.StoreName} và đưa lên bản đồ.";
        return RedirectToAction(nameof(Stores));
    }

    private static string BuildPoiDescription(AdminManagementApiClient.AdminStoreRegistrationDto store)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(store.Category))
        {
            parts.Add($"Loại hình: {store.Category.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(store.Address))
        {
            parts.Add($"Địa chỉ: {store.Address.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(store.OwnerName) || !string.IsNullOrWhiteSpace(store.Phone))
        {
            var owner = string.IsNullOrWhiteSpace(store.OwnerName) ? "N/A" : store.OwnerName.Trim();
            var phone = string.IsNullOrWhiteSpace(store.Phone) ? "N/A" : store.Phone.Trim();
            parts.Add($"Liên hệ: {owner} - {phone}");
        }

        if (!string.IsNullOrWhiteSpace(store.Description))
        {
            parts.Add($"Mô tả: {store.Description.Trim()}");
        }

        return parts.Count == 0 ? "Quán ăn do người bán gửi đăng ký." : string.Join(" | ", parts);
    }

    private static List<string> BuildOwnedStores(
        AdminManagementApiClient.AdminUserDto user,
        List<AdminManagementApiClient.AdminStoreRegistrationDto> pendingStores,
        List<POI> restaurants)
    {
        var owned = new List<string>();

        foreach (var pendingStore in pendingStores.Where(item =>
                     string.Equals(item.OwnerName?.Trim(), user.Name?.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            owned.Add($"{pendingStore.StoreName} (chờ duyệt)");
        }

        foreach (var restaurant in restaurants)
        {
            var ownerName = ExtractOwnerNameFromPoiDescription(restaurant.Description);
            if (string.Equals(ownerName, user.Name?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                owned.Add(restaurant.Name);
            }
        }

        return owned.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item).ToList();
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
}
