using System.Text.Json;
using Microsoft.Maui.Storage;
using SharedLib.Models;

namespace MobileApp.Services;

public class AuthService
{
    private const string UsersKey = "zes_users_v1";
    private const string CurrentUserIdKey = "zes_current_user_id_v1";

    private List<User>? _cachedUsers;

    public User? CurrentUser { get; private set; }

    public AuthService()
    {
        RefreshCurrentUser();
    }

    public IReadOnlyList<User> RegisteredUsers => LoadUsers();

    public Task<(bool Success, string Message)> RegisterAsync(string name, string username, string email, string password)
    {
        name = name.Trim();
        username = username.Trim();
        email = email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return Task.FromResult((false, "Vui lòng nhập đủ thông tin."));
        }

        var users = LoadUsers();
        if (users.Any(user => user.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult((false, "Tên đăng nhập đã tồn tại."));
        }

        if (users.Any(user => user.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult((false, "Email đã được sử dụng."));
        }

        var nextId = users.Count == 0 ? 1 : users.Max(user => user.Id) + 1;
        users.Add(new User
        {
            Id = nextId,
            Name = name,
            Username = username,
            Email = email,
            Password = password
        });

        SaveUsers(users);
        return Task.FromResult((true, "Đăng ký thành công. Bạn có thể đăng nhập ngay."));
    }

    public Task<(bool Success, string Message)> LoginAsync(string identifier, string password)
    {
        identifier = identifier.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
        {
            return Task.FromResult((false, "Vui lòng nhập tên đăng nhập/email và mật khẩu."));
        }

        var user = LoadUsers().FirstOrDefault(item =>
            item.Username.Equals(identifier, StringComparison.OrdinalIgnoreCase) ||
            item.Email.Equals(identifier, StringComparison.OrdinalIgnoreCase));

        if (user is null || !user.Password.Equals(password, StringComparison.Ordinal))
        {
            return Task.FromResult((false, "Sai thông tin đăng nhập."));
        }

        CurrentUser = user;
        Preferences.Default.Set(CurrentUserIdKey, user.Id);
        return Task.FromResult((true, "Đăng nhập thành công."));
    }

    public Task SignOutAsync()
    {
        CurrentUser = null;
        Preferences.Default.Remove(CurrentUserIdKey);
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

    private List<User> LoadUsers()
    {
        if (_cachedUsers is not null)
        {
            return _cachedUsers;
        }

        var json = Preferences.Default.Get(UsersKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            _cachedUsers = new List<User>();
            return _cachedUsers;
        }

        try
        {
            _cachedUsers = JsonSerializer.Deserialize<List<User>>(json) ?? new List<User>();
        }
        catch
        {
            _cachedUsers = new List<User>();
        }

        return _cachedUsers;
    }

    private void SaveUsers(List<User> users)
    {
        _cachedUsers = users;
        Preferences.Default.Set(UsersKey, JsonSerializer.Serialize(users));
    }

    private void RefreshCurrentUser()
    {
        var users = LoadUsers();
        var currentUserId = Preferences.Default.Get(CurrentUserIdKey, -1);
        CurrentUser = users.FirstOrDefault(user => user.Id == currentUserId);
    }
}