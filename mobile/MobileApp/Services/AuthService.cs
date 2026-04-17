using Microsoft.Maui.Storage;
using SharedLib.Models;
using System.IO;

namespace MobileApp.Services;

public class AuthService
{
    private const string CurrentUserKey = "zes_current_user_v2";
    private const string MenuWelcomeImagePathKeyPrefix = "zes_menu_welcome_image_v1";
    private const string MenuWelcomeCropXKeyPrefix = "zes_menu_welcome_crop_x_v1";
    private const string MenuWelcomeCropYKeyPrefix = "zes_menu_welcome_crop_y_v1";
    private const string BuiltInAssetPrefix = "asset:";
    private readonly ApiService _apiService;

    private static readonly HashSet<string> BuiltInMenuWelcomeBackgroundAssets = new(StringComparer.OrdinalIgnoreCase)
    {
        "menu_welcome_bg_aurora.svg",
        "menu_welcome_bg_neon_grid.svg",
        "menu_welcome_bg_sunset_glass.svg",
        "menu_welcome_bg_soft_mesh.svg",
        "menu_welcome_bg_ocean_wave.svg",
        "menu_welcome_bg_urban_night.svg",
        "menu_welcome_bg_mint_flow.svg",
        "menu_welcome_bg_rose_tech.svg"
    };

    public User? CurrentUser { get; private set; }

    public AuthService(ApiService apiService)
    {
        _apiService = apiService;
        RefreshCurrentUser();
    }

    public IReadOnlyList<User> RegisteredUsers => CurrentUser is null ? Array.Empty<User>() : new[] { CurrentUser };

    public async Task<(bool Success, string Message)> RegisterAsync(string name, string username, string email, string password, bool registerAsSeller = false)
    {
        var role = registerAsSeller ? "seller" : "customer";
        return await _apiService.RegisterAsync(name, email, username, password, role);
    }

    public async Task<(bool Success, string Message)> LoginAsync(string identifier, string password)
    {
        var result = await _apiService.LoginAsync(identifier, password);
        if (!result.Success || result.User is null)
        {
            return (false, result.Message);
        }

        CurrentUser = result.User;
        Preferences.Default.Set(CurrentUserKey, System.Text.Json.JsonSerializer.Serialize(CurrentUser));
        return (true, result.Message);
    }

    public async Task<(bool Success, string Message)> UpdateCurrentUserAvatarAsync(byte[] imageBytes, string fileName, string? contentType = null)
    {
        if (CurrentUser is null)
        {
            return (false, "Bạn cần đăng nhập trước.");
        }

        var upload = await _apiService.UploadUserAvatarAsync(imageBytes, fileName, contentType);
        if (!upload.Success || string.IsNullOrWhiteSpace(upload.ImageUrl))
        {
            return (false, upload.Message);
        }

        var update = await _apiService.UpdateUserAvatarAsync(CurrentUser.Id, upload.ImageUrl);
        if (!update.Success)
        {
            return (false, update.Message);
        }

        CurrentUser.AvatarUrl = upload.ImageUrl.Trim();
        Preferences.Default.Set(CurrentUserKey, System.Text.Json.JsonSerializer.Serialize(CurrentUser));
        return (true, update.Message);
    }

    public void ClearMenuWelcomeImage()
    {
        if (CurrentUser is null)
        {
            return;
        }

        var key = BuildMenuWelcomeImagePreferenceKey(CurrentUser.Id);
        var source = Preferences.Default.Get(key, string.Empty);
        Preferences.Default.Remove(key);
        Preferences.Default.Remove(BuildMenuWelcomeCropXPreferenceKey(CurrentUser.Id));
        Preferences.Default.Remove(BuildMenuWelcomeCropYPreferenceKey(CurrentUser.Id));

        DeleteLocalBackgroundFileIfNeeded(source, null);
    }

    public (string? Source, bool IsBuiltInAsset) GetMenuWelcomeImageSource()
    {
        if (CurrentUser is null)
        {
            return (null, false);
        }

        var key = BuildMenuWelcomeImagePreferenceKey(CurrentUser.Id);
        var value = Preferences.Default.Get(key, string.Empty);
        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, false);
        }

        if (value.StartsWith(BuiltInAssetPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return (value[BuiltInAssetPrefix.Length..], true);
        }

        return (value, false);
    }

    public (bool Success, string Message) UseBuiltInMenuWelcomeImage(string assetFileName)
    {
        if (CurrentUser is null)
        {
            return (false, "Bạn cần đăng nhập trước.");
        }

        if (string.IsNullOrWhiteSpace(assetFileName) || !BuiltInMenuWelcomeBackgroundAssets.Contains(assetFileName))
        {
            return (false, "Mẫu nền không hợp lệ.");
        }

        var key = BuildMenuWelcomeImagePreferenceKey(CurrentUser.Id);
        var previousSource = Preferences.Default.Get(key, string.Empty);

        Preferences.Default.Set(key, $"{BuiltInAssetPrefix}{assetFileName}");
        SetMenuWelcomeCropOffset(0f, 0f);
        DeleteLocalBackgroundFileIfNeeded(previousSource, null);

        return (true, "Đã áp dụng mẫu nền thẻ chào.");
    }

    public void SetMenuWelcomeCropOffset(float normalizedOffsetX, float normalizedOffsetY)
    {
        if (CurrentUser is null)
        {
            return;
        }

        var clampedX = Math.Clamp(normalizedOffsetX, -1f, 1f);
        var clampedY = Math.Clamp(normalizedOffsetY, -1f, 1f);

        Preferences.Default.Set(BuildMenuWelcomeCropXPreferenceKey(CurrentUser.Id), clampedX);
        Preferences.Default.Set(BuildMenuWelcomeCropYPreferenceKey(CurrentUser.Id), clampedY);
    }

    public (float OffsetX, float OffsetY) GetMenuWelcomeCropOffset()
    {
        if (CurrentUser is null)
        {
            return (0f, 0f);
        }

        var offsetX = Preferences.Default.Get(BuildMenuWelcomeCropXPreferenceKey(CurrentUser.Id), 0f);
        var offsetY = Preferences.Default.Get(BuildMenuWelcomeCropYPreferenceKey(CurrentUser.Id), 0f);

        return (Math.Clamp(offsetX, -1f, 1f), Math.Clamp(offsetY, -1f, 1f));
    }

    public Task SignOutAsync()
    {
        CurrentUser = null;
        Preferences.Default.Remove(CurrentUserKey);
        return Task.CompletedTask;
    }

    public string GetInitials(User? user)
    {
        var source = string.IsNullOrWhiteSpace(user?.Name) ? user?.Username : user?.Name;
        if (string.IsNullOrWhiteSpace(source))
        {
            return "U";
        }

        var parts = source.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return parts[0].Substring(0, 1).ToUpperInvariant();
        }

        return string.Concat(parts[0][0], parts[^1][0]).ToUpperInvariant();
    }

    private void RefreshCurrentUser()
    {
        var json = Preferences.Default.Get(CurrentUserKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            CurrentUser = null;
            return;
        }

        try
        {
            CurrentUser = System.Text.Json.JsonSerializer.Deserialize<User>(json);
        }
        catch
        {
            CurrentUser = null;
        }
    }

    private static string BuildMenuWelcomeImagePreferenceKey(int userId)
    {
        return $"{MenuWelcomeImagePathKeyPrefix}_{userId}";
    }

    private static string BuildMenuWelcomeCropXPreferenceKey(int userId)
    {
        return $"{MenuWelcomeCropXKeyPrefix}_{userId}";
    }

    private static string BuildMenuWelcomeCropYPreferenceKey(int userId)
    {
        return $"{MenuWelcomeCropYKeyPrefix}_{userId}";
    }

    private static void DeleteLocalBackgroundFileIfNeeded(string? sourceValue, string? keepPath)
    {
        if (string.IsNullOrWhiteSpace(sourceValue) || sourceValue.StartsWith(BuiltInAssetPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(keepPath) && string.Equals(sourceValue, keepPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!File.Exists(sourceValue))
        {
            return;
        }

        try
        {
            File.Delete(sourceValue);
        }
        catch
        {
            // Ignore file cleanup errors to avoid breaking user flow.
        }
    }
}