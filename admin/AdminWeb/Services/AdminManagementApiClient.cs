using System.Net.Http.Json;

namespace AdminWeb.Services;

public class AdminManagementApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AdminManagementApiClient> _logger;

    public string? LastUsersError { get; private set; }
    public string? LastStoreRegistrationsError { get; private set; }

    public AdminManagementApiClient(HttpClient httpClient, ILogger<AdminManagementApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<AdminUserDto>> GetUsersAsync()
    {
        LastUsersError = null;
        try
        {
            return await _httpClient.GetFromJsonAsync<List<AdminUserDto>>("api/auth/users") ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot load user list from backend API.");
            LastUsersError = "Không thể tải danh sách user từ backend API.";
            return [];
        }
    }

    public async Task<(bool Success, string Message)> UpdateUserRoleAsync(int userId, string role)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/auth/users/{userId}/role", new { Role = role });
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            if (response.IsSuccessStatusCode)
            {
                return (true, payload?.Message ?? "Cập nhật role thành công.");
            }

            return (false, payload?.Message ?? "Cập nhật role thất bại.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot update role for user {UserId}.", userId);
            return (false, "Không thể kết nối backend API.");
        }
    }

    public async Task<(bool Success, string Message)> CreateUserAsync(AdminUserUpsertDto user)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/users", user);
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            if (response.IsSuccessStatusCode)
            {
                return (true, payload?.Message ?? "Đã tạo người dùng mới.");
            }

            return (false, payload?.Message ?? "Tạo người dùng thất bại.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot create user on backend API.");
            return (false, "Không thể kết nối backend API.");
        }
    }

    public async Task<(bool Success, string Message)> UpdateUserAsync(int userId, AdminUserUpsertDto user)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/auth/users/{userId}", user);
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            if (response.IsSuccessStatusCode)
            {
                return (true, payload?.Message ?? "Cập nhật người dùng thành công.");
            }

            return (false, payload?.Message ?? "Cập nhật người dùng thất bại.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot update user {UserId} on backend API.", userId);
            return (false, "Không thể kết nối backend API.");
        }
    }

    public async Task<(bool Success, string Message)> DeleteUserAsync(int userId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/auth/users/{userId}");
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            if (response.IsSuccessStatusCode)
            {
                return (true, payload?.Message ?? "Đã xóa người dùng.");
            }

            return (false, payload?.Message ?? "Xóa người dùng thất bại.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot delete user {UserId}.", userId);
            return (false, "Không thể kết nối backend API.");
        }
    }

    public async Task<List<AdminStoreRegistrationDto>> GetStoreRegistrationsAsync()
    {
        LastStoreRegistrationsError = null;
        try
        {
            return await _httpClient.GetFromJsonAsync<List<AdminStoreRegistrationDto>>("api/store-registrations") ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot load store registrations from backend API.");
            LastStoreRegistrationsError = "Không thể tải danh sách đăng ký quán ăn từ backend API.";
            return [];
        }
    }

    public async Task<(bool Success, string Message)> DeleteStoreRegistrationAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/store-registrations/{id}");
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            if (response.IsSuccessStatusCode)
            {
                return (true, payload?.Message ?? "Đã xóa đăng ký cửa hàng.");
            }

            return (false, payload?.Message ?? "Xóa đăng ký cửa hàng thất bại.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot delete store registration {StoreRegistrationId}.", id);
            return (false, "Không thể kết nối backend API.");
        }
    }

    public async Task<(bool Success, string Message)> UpdateStoreRegistrationAsync(int id, AdminStoreRegistrationUpsertDto request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/store-registrations/{id}", request);
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            if (response.IsSuccessStatusCode)
            {
                return (true, payload?.Message ?? "Cập nhật đăng ký cửa hàng thành công.");
            }

            return (false, payload?.Message ?? "Cập nhật đăng ký cửa hàng thất bại.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot update store registration {StoreRegistrationId}.", id);
            return (false, "Không thể kết nối backend API.");
        }
    }

    public sealed class AdminUserDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = "customer";
    }

    public sealed class AdminStoreRegistrationDto
    {
        public int Id { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime SubmittedAtUtc { get; set; }
    }

    public sealed class AdminStoreRegistrationUpsertDto
    {
        public string StoreName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public sealed class AdminUserUpsertDto
    {
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "customer";
    }

    private sealed class ApiMessageResponse
    {
        public string Message { get; set; } = string.Empty;
    }
}
