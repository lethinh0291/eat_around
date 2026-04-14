using System.Net.Http.Json;
using System.Text.Json;

namespace AdminWeb.Services;

public class AdminManagementApiClient
{
    private const string CloudinaryCloudName = "dmpaepela";
    private const string CloudinaryUploadPreset = "zestour";

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

    public async Task<LoginResultDto?> AuthenticateAsync(string usernameOrEmail, string password)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", new
            {
                Username = usernameOrEmail,
                Password = password
            });

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<LoginApiResponse>();
            if (payload?.User is null)
            {
                return null;
            }

            return new LoginResultDto
            {
                Id = payload.User.Id,
                Name = payload.User.Name?.Trim() ?? string.Empty,
                Username = payload.User.Username?.Trim() ?? string.Empty,
                Email = payload.User.Email?.Trim() ?? string.Empty,
                Role = string.IsNullOrWhiteSpace(payload.User.Role)
                    ? "customer"
                    : payload.User.Role.Trim().ToLowerInvariant()
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot authenticate user against backend API.");
            return null;
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

    public async Task<List<AdBannerDto>> GetAdBannersAsync()
    {
        try
        {
            var items = await _httpClient.GetFromJsonAsync<List<AdBannerDto>>("api/ad-banners") ?? [];
            foreach (var item in items)
            {
                item.ImageUrl = NormalizeBackendFileUrl(item.ImageUrl);
            }

            return items;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot load ad banners from backend API.");
            return [];
        }
    }

    public async Task<AdBannerDto?> GetAdBannerByIdAsync(int id)
    {
        try
        {
            var item = await _httpClient.GetFromJsonAsync<AdBannerDto>($"api/ad-banners/{id}");
            if (item is not null)
            {
                item.ImageUrl = NormalizeBackendFileUrl(item.ImageUrl);
            }

            return item;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot load ad banner {AdBannerId} from backend API.", id);
            return null;
        }
    }

    public async Task<(bool Success, string Message, string? ImageUrl)> UploadAdBannerImageAsync(Stream stream, string fileName, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(CloudinaryCloudName) ||
            string.IsNullOrWhiteSpace(CloudinaryUploadPreset) ||
            CloudinaryCloudName.Contains("YOUR_", StringComparison.OrdinalIgnoreCase) ||
            CloudinaryUploadPreset.Contains("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Cloudinary chưa được cấu hình cho admin upload banner.", null);
        }

        try
        {
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(stream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim());
            content.Add(streamContent, "file", string.IsNullOrWhiteSpace(fileName) ? "banner.jpg" : fileName);
            content.Add(new StringContent(CloudinaryUploadPreset), "upload_preset");
            content.Add(new StringContent("zestour/banners"), "folder");

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var preset = Uri.EscapeDataString(CloudinaryUploadPreset);
            var folder = Uri.EscapeDataString("zestour/banners");
            var endpoint = $"https://api.cloudinary.com/v1_1/{CloudinaryCloudName}/image/upload?upload_preset={preset}&folder={folder}";

            var response = await client.PostAsync(endpoint, content);
            var payloadJson = await response.Content.ReadAsStringAsync();
            var payload = JsonSerializer.Deserialize<UploadImageResponse>(payloadJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (!response.IsSuccessStatusCode)
            {
                return (false, payload?.Error?.Message ?? "Không thể tải ảnh banner lên cloud.", null);
            }

            var imageUrl = payload?.SecureUrl?.Trim();
            return (true, "Tải ảnh banner thành công.", imageUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot upload banner image to Cloudinary.");
            return (false, "Không thể upload ảnh banner lên cloud.", null);
        }
    }

    public async Task<(bool Success, string Message)> CreateAdBannerAsync(AdBannerUpsertDto request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/ad-banners", request);
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            if (response.IsSuccessStatusCode)
            {
                return (true, payload?.Message ?? "Đã thêm banner.");
            }

            return (false, payload?.Message ?? "Không thể thêm banner.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot create ad banner on backend API.");
            return (false, "Không thể kết nối backend API.");
        }
    }

    public async Task<(bool Success, string Message)> UpdateAdBannerAsync(int id, AdBannerUpsertDto request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/ad-banners/{id}", request);
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            if (response.IsSuccessStatusCode)
            {
                return (true, payload?.Message ?? "Đã cập nhật banner.");
            }

            return (false, payload?.Message ?? "Không thể cập nhật banner.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot update ad banner {AdBannerId} on backend API.", id);
            return (false, "Không thể kết nối backend API.");
        }
    }

    public async Task<(bool Success, string Message)> DeleteAdBannerAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/ad-banners/{id}");
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            if (response.IsSuccessStatusCode)
            {
                return (true, payload?.Message ?? "Đã xóa banner.");
            }

            return (false, payload?.Message ?? "Không thể xóa banner.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot delete ad banner {AdBannerId} from backend API.", id);
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
        public List<string>? ImageUrls { get; set; }
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
        public List<string>? ImageUrls { get; set; }
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

    public sealed class AdBannerDto
    {
        public int Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    public sealed class AdBannerUpsertDto
    {
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
    }

    public sealed class LoginResultDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "customer";
    }

    private sealed class ApiMessageResponse
    {
        public string Message { get; set; } = string.Empty;
    }

    private sealed class LoginApiResponse
    {
        public string Message { get; set; } = string.Empty;
        public LoginUserResponse? User { get; set; }
    }

    private sealed class LoginUserResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = "customer";
    }

    private sealed class UploadImageResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("secure_url")]
        public string? SecureUrl { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("error")]
        public CloudinaryErrorResponse? Error { get; set; }
    }

    private sealed class CloudinaryErrorResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    private string NormalizeBackendFileUrl(string? url)
    {
        var value = url?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            return value;
        }

        if (_httpClient.BaseAddress is null)
        {
            return value;
        }

        return new Uri(_httpClient.BaseAddress, value.TrimStart('/')).ToString();
    }
}
