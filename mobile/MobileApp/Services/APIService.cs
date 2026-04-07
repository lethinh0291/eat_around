using System.Net.Http.Json;
using SharedLib.Models;

namespace MobileApp.Services;

public class ApiService
{
    // Đăng ký
    public async Task<bool> RegisterAsync(string name, string email, string username, string password)
    {
        var newUser = new SharedLib.Models.User
        {
            Name = name,
            Email = email,
            Username = username,
            Password = password
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/register", newUser);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Đăng ký bị lỗi: {ex.Message}");

            return false;
        }
    }
    // Đăng nhập
    public async Task<bool> LoginAsync(string username, string password)
    {
        var loginData = new { Username = username, Password = password };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginData);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Đăng nhập bị lỗi: {ex.Message}");
            return false;
        }
    }
    private readonly HttpClient _httpClient;
    // Dùng 10.0.2.2 nếu dùng máy ảo Android, hoặc IP máy tính nếu dùng máy thật
    private const string BaseUrl = "http://10.0.2.2:5069/api/";

    public ApiService()
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
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


}