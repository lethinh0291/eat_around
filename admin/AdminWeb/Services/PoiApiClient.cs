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

    public async Task<(bool Success, string Message)> GenerateQrAsync(int poiId, string languageCode)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/poi/{poiId}/generate-qr?languageCode={Uri.EscapeDataString(languageCode)}", null);
            if (response.IsSuccessStatusCode)
            {
                return (true, "Tao QR thanh cong.");
            }

            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            return (false, payload?.Message ?? "Khong the tao QR cho POI.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot generate QR for POI {PoiId}.", poiId);
            return (false, "Khong the ket noi backend API.");
        }
    }

    public async Task<List<PoiQrTriggerSummaryDto>> GetQrTriggersForPoiAsync(int poiId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<PoiQrTriggerSummaryDto>>($"api/poi/{poiId}/qr-triggers") ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot load QR triggers for POI {PoiId}.", poiId);
            return [];
        }
    }

    public async Task<PoiQrTriggerDetailDto?> GetQrTriggerByIdAsync(int qrId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<PoiQrTriggerDetailDto>($"api/poi/qr-triggers/{qrId}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot load QR trigger {QrId}.", qrId);
            return null;
        }
    }

    public sealed class PoiQrTriggerSummaryDto
    {
        public int Id { get; set; }
        public string QrContent { get; set; } = string.Empty;
        public string LanguageCode { get; set; } = "vi";
        public int ScanCount { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string? ImagePreview { get; set; }
    }

    public sealed class PoiQrTriggerDetailDto
    {
        public int Id { get; set; }
        public int PoiId { get; set; }
        public string QrContent { get; set; } = string.Empty;
        public string LanguageCode { get; set; } = "vi";
        public string QrImageBase64 { get; set; } = string.Empty;
        public int ScanCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    private sealed class ApiMessageResponse
    {
        public string Message { get; set; } = string.Empty;
    }
}
