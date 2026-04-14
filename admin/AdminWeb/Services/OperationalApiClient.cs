using System.Net.Http.Json;

namespace AdminWeb.Services;

public class OperationalApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OperationalApiClient> _logger;

    public OperationalApiClient(HttpClient httpClient, ILogger<OperationalApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<ListenLogDto>> GetListenLogsAsync(DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var query = BuildDateQuery(fromUtc, toUtc);
        return await GetListAsync<ListenLogDto>($"api/listen-logs{query}");
    }

    public async Task<List<HeatmapPointDto>> GetHeatmapAsync(DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var query = BuildDateQuery(fromUtc, toUtc);
        return await GetListAsync<HeatmapPointDto>($"api/listen-logs/heatmap{query}");
    }

    public async Task<List<TopPoiListenDto>> GetTopPoisAsync(DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var query = BuildDateQuery(fromUtc, toUtc);
        return await GetListAsync<TopPoiListenDto>($"api/listen-logs/top-pois{query}");
    }

    public async Task<List<LanguageRatioDto>> GetLanguageRatiosAsync(DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var query = BuildDateQuery(fromUtc, toUtc);
        return await GetListAsync<LanguageRatioDto>($"api/listen-logs/language-ratio{query}");
    }

    public async Task<List<SystemLogDto>> GetSystemLogsAsync(DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var query = BuildDateQuery(fromUtc, toUtc);
        return await GetListAsync<SystemLogDto>($"api/system-logs{query}");
    }

    public async Task<(bool Success, string Message)> CreateSystemLogAsync(CreateSystemLogRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/system-logs", request);
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            return (response.IsSuccessStatusCode, payload?.Message ?? (response.IsSuccessStatusCode ? "Đã tạo log." : "Không thể tạo log."));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot create system log via backend API.");
            return (false, "Không thể kết nối backend API.");
        }
    }

    public async Task<(bool Success, string Message)> DeleteSystemLogAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/system-logs/{id}");
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            return (response.IsSuccessStatusCode, payload?.Message ?? (response.IsSuccessStatusCode ? "Đã xóa log." : "Không thể xóa log."));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot delete system log {LogId} via backend API.", id);
            return (false, "Không thể kết nối backend API.");
        }
    }

    public async Task<List<TourSummaryDto>> GetToursAsync()
    {
        return await GetListAsync<TourSummaryDto>("api/tours");
    }

    public async Task<TourDetailDto?> GetTourByIdAsync(int id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<TourDetailDto>($"api/tours/{id}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot load tour {TourId} from backend API.", id);
            return null;
        }
    }

    public async Task<(bool Success, string Message)> CreateTourAsync(UpsertTourRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/tours", request);
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            return (response.IsSuccessStatusCode, payload?.Message ?? (response.IsSuccessStatusCode ? "Đã tạo tour." : "Không thể tạo tour."));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot create tour via backend API.");
            return (false, "Không thể kết nối backend API.");
        }
    }

    public async Task<(bool Success, string Message)> UpdateTourAsync(int id, UpsertTourRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/tours/{id}", request);
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            return (response.IsSuccessStatusCode, payload?.Message ?? (response.IsSuccessStatusCode ? "Đã cập nhật tour." : "Không thể cập nhật tour."));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot update tour {TourId} via backend API.", id);
            return (false, "Không thể kết nối backend API.");
        }
    }

    public async Task<(bool Success, string Message)> DeleteTourAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/tours/{id}");
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            return (response.IsSuccessStatusCode, payload?.Message ?? (response.IsSuccessStatusCode ? "Đã xóa tour." : "Không thể xóa tour."));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot delete tour {TourId} via backend API.", id);
            return (false, "Không thể kết nối backend API.");
        }
    }

    public async Task<(bool Success, string Message)> ReplaceTourStopsAsync(int id, ReplaceTourStopsRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/tours/{id}/stops", request);
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            return (response.IsSuccessStatusCode, payload?.Message ?? (response.IsSuccessStatusCode ? "Đã cập nhật điểm dừng tour." : "Không thể cập nhật điểm dừng tour."));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot replace stops for tour {TourId} via backend API.", id);
            return (false, "Không thể kết nối backend API.");
        }
    }

    public async Task<List<PoiTranslationDto>> GetPoiTranslationsAsync(int? poiId = null, string? languageCode = null, string? status = null)
    {
        var queryParts = new List<string>();

        if (poiId.HasValue)
        {
            queryParts.Add($"poiId={poiId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            queryParts.Add($"languageCode={Uri.EscapeDataString(languageCode.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            queryParts.Add($"status={Uri.EscapeDataString(status.Trim())}");
        }

        var query = queryParts.Count == 0 ? string.Empty : $"?{string.Join("&", queryParts)}";
        return await GetListAsync<PoiTranslationDto>($"api/poi-translations{query}");
    }

    public async Task<(bool Success, string Message)> CreatePoiTranslationAsync(UpsertPoiTranslationRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/poi-translations", request);
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            return (response.IsSuccessStatusCode, payload?.Message ?? (response.IsSuccessStatusCode ? "Đã tạo bản dịch." : "Không thể tạo bản dịch."));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot create POI translation via backend API.");
            return (false, "Không thể kết nối backend API.");
        }
    }

    public async Task<(bool Success, string Message)> UpdatePoiTranslationAsync(int id, UpsertPoiTranslationRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/poi-translations/{id}", request);
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            return (response.IsSuccessStatusCode, payload?.Message ?? (response.IsSuccessStatusCode ? "Đã cập nhật bản dịch." : "Không thể cập nhật bản dịch."));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot update POI translation {TranslationId} via backend API.", id);
            return (false, "Không thể kết nối backend API.");
        }
    }

    public async Task<(bool Success, string Message)> UpdatePoiTranslationStatusAsync(int id, UpdatePoiTranslationStatusRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/poi-translations/{id}/status", request);
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            return (response.IsSuccessStatusCode, payload?.Message ?? (response.IsSuccessStatusCode ? "Đã cập nhật trạng thái bản dịch." : "Không thể cập nhật trạng thái bản dịch."));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot update translation status {TranslationId} via backend API.", id);
            return (false, "Không thể kết nối backend API.");
        }
    }

    public async Task<(bool Success, string Message)> DeletePoiTranslationAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/poi-translations/{id}");
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            return (response.IsSuccessStatusCode, payload?.Message ?? (response.IsSuccessStatusCode ? "Đã xóa bản dịch." : "Không thể xóa bản dịch."));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot delete POI translation {TranslationId} via backend API.", id);
            return (false, "Không thể kết nối backend API.");
        }
    }

    private async Task<List<T>> GetListAsync<T>(string path)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<T>>(path) ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Cannot load resource {Path} from backend API.", path);
            return [];
        }
    }

    private static string BuildDateQuery(DateTime? fromUtc, DateTime? toUtc)
    {
        var parts = new List<string>();
        if (fromUtc.HasValue)
        {
            parts.Add($"fromUtc={Uri.EscapeDataString(fromUtc.Value.ToString("O"))}");
        }

        if (toUtc.HasValue)
        {
            parts.Add($"toUtc={Uri.EscapeDataString(toUtc.Value.ToString("O"))}");
        }

        return parts.Count == 0 ? string.Empty : $"?{string.Join("&", parts)}";
    }

    public sealed class ListenLogDto
    {
        public int Id { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public int PoiId { get; set; }
        public string LanguageCode { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public double DurationSeconds { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime PlayedAtUtc { get; set; }
    }

    public sealed class HeatmapPointDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Count { get; set; }
    }

    public sealed class TopPoiListenDto
    {
        public int PoiId { get; set; }
        public string PoiName { get; set; } = string.Empty;
        public int Listens { get; set; }
        public double Percent { get; set; }
    }

    public sealed class LanguageRatioDto
    {
        public string LanguageCode { get; set; } = string.Empty;
        public int Listens { get; set; }
        public double Percent { get; set; }
    }

    public sealed class SystemLogDto
    {
        public int Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public sealed class CreateSystemLogRequest
    {
        public string Category { get; set; } = "system";
        public string Level { get; set; } = "info";
        public string Source { get; set; } = "admin";
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
    }

    public sealed class TourSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public bool IsActive { get; set; }
        public int StopCount { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    public sealed class TourDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public List<TourStopDto> Stops { get; set; } = [];
    }

    public sealed class TourStopDto
    {
        public int Id { get; set; }
        public int PoiId { get; set; }
        public string PoiName { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string? Note { get; set; }
    }

    public sealed class UpsertTourRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public sealed class ReplaceTourStopsRequest
    {
        public List<TourStopRequest> Stops { get; set; } = [];
    }

    public sealed class TourStopRequest
    {
        public int PoiId { get; set; }
        public int SortOrder { get; set; }
        public string? Note { get; set; }
    }

    public sealed class PoiTranslationDto
    {
        public int Id { get; set; }
        public int PoiId { get; set; }
        public string PoiName { get; set; } = string.Empty;
        public string LanguageCode { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string ContentText { get; set; } = string.Empty;
        public string? AudioUrl { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? SubmittedBy { get; set; }
        public string? ReviewedBy { get; set; }
        public DateTime SubmittedAtUtc { get; set; }
        public DateTime? ReviewedAtUtc { get; set; }
    }

    public sealed class UpsertPoiTranslationRequest
    {
        public int PoiId { get; set; }
        public string? LanguageCode { get; set; }
        public string? Title { get; set; }
        public string ContentText { get; set; } = string.Empty;
        public string? AudioUrl { get; set; }
        public string? SubmittedBy { get; set; }
    }

    public sealed class UpdatePoiTranslationStatusRequest
    {
        public string Status { get; set; } = "pending";
        public string? ReviewedBy { get; set; }
    }

    private sealed class ApiMessageResponse
    {
        public string Message { get; set; } = string.Empty;
    }
}
