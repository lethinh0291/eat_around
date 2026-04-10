using System.Net.Http.Json;
using SharedLib.Models;

namespace AdminWeb.Services;

public class PoiApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PoiApiClient> _logger;

    public PoiApiClient(HttpClient httpClient, ILogger<PoiApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<POI>> GetAllAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<POI>>("api/poi") ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot load POI list from backend API.");
            return [];
        }
    }

    public async Task<POI?> GetByIdAsync(int id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<POI>($"api/poi/{id}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot load POI with id {PoiId} from backend API.", id);
            return null;
        }
    }

    public async Task<bool> CreateAsync(POI poi)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/poi", poi);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot create POI on backend API.");
            return false;
        }
    }

    public async Task<bool> UpdateAsync(POI poi)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/poi/{poi.Id}", poi);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot update POI {PoiId} on backend API.", poi.Id);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/poi/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot delete POI {PoiId} on backend API.", id);
            return false;
        }
    }
}
