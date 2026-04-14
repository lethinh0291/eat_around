using Microsoft.Maui.Storage;
using SharedLib.Models;

namespace MobileApp.Services;

public class AuthService
{
    private const string CurrentUserKey = "zes_current_user_v2";
    private readonly ApiService _apiService;

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
}