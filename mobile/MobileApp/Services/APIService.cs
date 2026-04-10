using System.Net.Http.Json;
using SharedLib.Models;
using BackendStoreRegistration = MobileApp.Services.ApiService.StoreRegistrationRequest;

namespace MobileApp.Services;

public class ApiService
{
    private const string CloudinaryCloudName = "dmpaepela";
    private const string CloudinaryUploadPreset = "zestour";

    public async Task<(bool Success, string Message)> RegisterAsync(string name, string email, string username, string password, string role = "customer")
    {
        var newUser = new SharedLib.Models.User
        {
            Name = name,
            Email = email,
            Username = username,
            Password = password,
            Role = role
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/register", newUser);
            if (response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
                return (true, payload?.Message ?? "Đăng ký thành công.");
            }

            var failedPayload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            return (false, failedPayload?.Message ?? "Đăng ký thất bại.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Đăng ký bị lỗi: {ex.Message}");

            return (false, "Không thể kết nối máy chủ đăng ký.");
        }
    }

    public async Task<(bool Success, string Message, User? User)> LoginAsync(string username, string password)
    {
        var loginData = new { Username = username, Password = password };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/login", loginData);
            if (response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
                return (true, payload?.Message ?? "Đăng nhập thành công.", payload?.User);
            }

            var failedPayload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            return (false, failedPayload?.Message ?? "Đăng nhập thất bại.", null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Đăng nhập bị lỗi: {ex.Message}");
            return (false, "Không thể kết nối máy chủ đăng nhập.", null);
        }
    }

    private readonly HttpClient _httpClient;
    // Dùng 10.0.2.2 nếu dùng máy ảo Android, hoặc IP máy tính nếu dùng máy thật
    private const string BaseUrl = "http://10.0.2.2:5069/api/";

    public ApiService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(8)
        };
    }

    public async Task<List<POI>> GetPoisAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<POI>>("poi") ?? new List<POI>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi khi lấy POIs từ API: {ex.Message}");
            return new List<POI>();
        }
    }

    public async Task<(bool Success, string Message)> SubmitStoreRegistrationAsync(
        string storeName,
        string ownerName,
        string phone,
        string address,
        string category,
        string description,
        string? imageUrl = null)
    {
        var payload = new BackendStoreRegistration
        {
            StoreName = storeName,
            OwnerName = ownerName,
            ImageUrl = imageUrl,
            Phone = phone,
            Address = address,
            Category = category,
            Description = description
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("store-registrations", payload);
            if (response.IsSuccessStatusCode)
            {
                var okPayload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
                return (true, okPayload?.Message ?? "Yêu cầu đăng ký cửa hàng đã được ghi nhận.");
            }

            var failedPayload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            return (false, failedPayload?.Message ?? "Gửi đăng ký cửa hàng thất bại.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi khi gửi đăng ký cửa hàng: {ex.Message}");
            return (false, "Không thể kết nối máy chủ. Vui lòng thử lại.");
        }
    }

    public async Task<List<ManagementUser>> GetUsersAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<ManagementUser>>("auth/users") ?? new List<ManagementUser>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi lấy danh sách người dùng: {ex.Message}");
            return new List<ManagementUser>();
        }
    }

    public async Task<(bool Success, string Message)> UpdateUserRoleAsync(int userId, string role)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"auth/users/{userId}/role", new { Role = role });
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();

            if (response.IsSuccessStatusCode)
            {
                return (true, payload?.Message ?? "Cập nhật role thành công.");
            }

            return (false, payload?.Message ?? "Cập nhật role thất bại.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi cập nhật role: {ex.Message}");
            return (false, "Không thể kết nối máy chủ.");
        }
    }

    public async Task<(bool Success, string Message)> DeleteUserAsync(int userId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"auth/users/{userId}");
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();

            if (response.IsSuccessStatusCode)
            {
                return (true, payload?.Message ?? "Đã xóa người dùng.");
            }

            return (false, payload?.Message ?? "Xóa người dùng thất bại.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi xóa người dùng: {ex.Message}");
            return (false, "Không thể kết nối máy chủ.");
        }
    }

    public async Task<List<ManagementStoreRegistration>> GetStoreRegistrationsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<ManagementStoreRegistration>>("store-registrations") ?? new List<ManagementStoreRegistration>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi lấy danh sách đăng ký quán ăn: {ex.Message}");
            return new List<ManagementStoreRegistration>();
        }
    }

    public async Task<List<ManagementStoreRegistration>> GetMyStoreRegistrationsAsync(string ownerName)
    {
        try
        {
            var encoded = Uri.EscapeDataString(ownerName?.Trim() ?? string.Empty);
            return await _httpClient.GetFromJsonAsync<List<ManagementStoreRegistration>>($"store-registrations/owner?ownerName={encoded}") ?? new List<ManagementStoreRegistration>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi lấy đăng ký cửa hàng của tôi: {ex.Message}");
            return new List<ManagementStoreRegistration>();
        }
    }

    public async Task<(bool Success, string Message)> UpdateMyStoreRegistrationAsync(ManagementStoreRegistration registration, string ownerName)
    {
        try
        {
            var payload = new
            {
                registration.StoreName,
                OwnerName = ownerName,
                registration.ImageUrl,
                registration.Phone,
                registration.Address,
                registration.Category,
                registration.Description
            };

            var response = await _httpClient.PutAsJsonAsync($"store-registrations/{registration.Id}", payload);
            var result = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            if (response.IsSuccessStatusCode)
            {
                return (true, result?.Message ?? "Cập nhật cửa hàng thành công.");
            }

            return (false, result?.Message ?? "Cập nhật cửa hàng thất bại.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi cập nhật đăng ký cửa hàng: {ex.Message}");
            return (false, "Không thể kết nối máy chủ.");
        }
    }

    public async Task<(bool Success, string Message)> DeleteMyStoreRegistrationAsync(int id, string ownerName)
    {
        try
        {
            var encoded = Uri.EscapeDataString(ownerName?.Trim() ?? string.Empty);
            var response = await _httpClient.DeleteAsync($"store-registrations/{id}?ownerName={encoded}");
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();

            if (response.IsSuccessStatusCode)
            {
                return (true, payload?.Message ?? "Đã xóa đăng ký cửa hàng.");
            }

            return (false, payload?.Message ?? "Xóa đăng ký cửa hàng thất bại.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi xóa đăng ký cửa hàng của tôi: {ex.Message}");
            return (false, "Không thể kết nối máy chủ.");
        }
    }

    public async Task<(bool Success, string Message)> DeleteStoreRegistrationAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"store-registrations/{id}");
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();

            if (response.IsSuccessStatusCode)
            {
                return (true, payload?.Message ?? "Đã xóa đăng ký cửa hàng.");
            }

            return (false, payload?.Message ?? "Xóa đăng ký cửa hàng thất bại.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi xóa đăng ký cửa hàng: {ex.Message}");
            return (false, "Không thể kết nối máy chủ.");
        }
    }

    public async Task<(bool Success, string Message, string? ImageUrl)> UploadStoreImageAsync(byte[] imageBytes, string fileName, string? contentType = null)
    {
        if (string.IsNullOrWhiteSpace(CloudinaryCloudName) ||
            string.IsNullOrWhiteSpace(CloudinaryUploadPreset) ||
            CloudinaryCloudName.Contains("YOUR_", StringComparison.OrdinalIgnoreCase) ||
            CloudinaryUploadPreset.Contains("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Cloudinary chưa được cấu hình. Vui lòng cập nhật CloudinaryCloudName và CloudinaryUploadPreset trong ApiService.", null);
        }

        try
        {
            if (imageBytes is null || imageBytes.Length == 0)
            {
                return (false, "Ảnh rỗng hoặc không hợp lệ.", null);
            }

            using var content = new MultipartFormDataContent();
            var byteContent = new ByteArrayContent(imageBytes);
            var normalizedContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
            byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(normalizedContentType);
            content.Add(byteContent, "file", string.IsNullOrWhiteSpace(fileName) ? "store.jpg" : fileName);
            content.Add(new StringContent(CloudinaryUploadPreset), "upload_preset");
            content.Add(new StringContent("zestour/stores"), "folder");

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var preset = Uri.EscapeDataString(CloudinaryUploadPreset);
            var folder = Uri.EscapeDataString("zestour/stores");
            var endpoint = $"https://api.cloudinary.com/v1_1/{CloudinaryCloudName}/image/upload?upload_preset={preset}&folder={folder}";
            var response = await client.PostAsync(endpoint, content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var cloudError = System.Text.Json.JsonSerializer.Deserialize<CloudinaryErrorEnvelope>(body);
                var detail = cloudError?.Error?.Message;
                if (string.IsNullOrWhiteSpace(detail))
                {
                    detail = $"HTTP {(int)response.StatusCode}";
                }

                return (false, $"Upload Cloudinary thất bại: {detail}", null);
            }

            var payload = System.Text.Json.JsonSerializer.Deserialize<CloudinaryUploadResponse>(body);
            if (string.IsNullOrWhiteSpace(payload?.SecureUrl))
            {
                return (false, "Không nhận được URL ảnh từ Cloudinary.", null);
            }

            return (true, "Upload ảnh thành công.", payload.SecureUrl);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi upload ảnh Cloudinary: {ex.Message}");
            return (false, $"Không thể upload ảnh cửa hàng lên cloud: {ex.Message}", null);
        }
    }

    private sealed class ApiMessageResponse
    {
        public string Message { get; set; } = string.Empty;
    }

    private sealed class LoginResponse
    {
        public string Message { get; set; } = string.Empty;
        public User? User { get; set; }
    }

    public sealed class StoreRegistrationRequest
    {
        public string StoreName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public sealed class ManagementUser
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = "customer";
    }

    public sealed class ManagementStoreRegistration
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

    private sealed class CloudinaryUploadResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("secure_url")]
        public string SecureUrl { get; set; } = string.Empty;
    }

    private sealed class CloudinaryErrorEnvelope
    {
        [System.Text.Json.Serialization.JsonPropertyName("error")]
        public CloudinaryErrorDetail? Error { get; set; }
    }

    private sealed class CloudinaryErrorDetail
    {
        [System.Text.Json.Serialization.JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }


}