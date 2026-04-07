using System.Net.Http.Json;
using SharedLib.Models;

namespace MobileApp.Services;

public class ApiService
{
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
        catch { return new List<POI>(); }
    }
}