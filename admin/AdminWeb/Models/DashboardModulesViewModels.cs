using AdminWeb.Services;
using SharedLib.Models;

namespace AdminWeb.Models;

public class DashboardOverviewViewModel
{
    public AdminDashboardViewModel Summary { get; set; } = new();
    public int TotalTours { get; set; }
    public int TotalTranslations { get; set; }
    public int TotalListenLogs { get; set; }
    public int TotalSystemLogs { get; set; }
    public int ListenLogsLast7Days { get; set; }
    public double AverageListenDurationSeconds { get; set; }
    public int AudioListenLogs { get; set; }
    public int TtsListenLogs { get; set; }
    public int TodayListenLogs { get; set; }
    public int WeekListenLogs { get; set; }
    public int DistinctListenLanguages { get; set; }
    public List<DashboardTimeCountViewModel> ListenByDay { get; set; } = [];
    public List<DashboardTimeCountViewModel> ListenByHour { get; set; } = [];
    public List<DashboardListenPoiViewModel> TopListenPois { get; set; } = [];
    public List<DashboardLanguageShareViewModel> TopListenLanguages { get; set; } = [];
    public List<DashboardRecentActivityViewModel> RecentActivities { get; set; } = [];
}

public class DashboardTimeCountViewModel
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DashboardRecentActivityViewModel
{
    public string TimeLabel { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Tone { get; set; } = "neutral";
}

public class DashboardListenPoiViewModel
{
    public int PoiId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public int Listens { get; set; }
    public double Percent { get; set; }
}

public class DashboardLanguageShareViewModel
{
    public string LanguageCode { get; set; } = string.Empty;
    public int Listens { get; set; }
    public double Percent { get; set; }
}

public class DashboardAnalyticsViewModel
{
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int? PoiId { get; set; }
    public string? LanguageCode { get; set; }
    public List<OperationalApiClient.ListenLogDto> ListenLogs { get; set; } = [];
    public List<OperationalApiClient.HeatmapPointDto> HeatmapPoints { get; set; } = [];
    public List<OperationalApiClient.TopPoiListenDto> TopPois { get; set; } = [];
    public List<OperationalApiClient.LanguageRatioDto> LanguageRatios { get; set; } = [];
    public List<POI> Pois { get; set; } = [];
    public List<string> Languages { get; set; } = [];
}

public class DashboardLogsViewModel
{
    public List<OperationalApiClient.ListenLogDto> ListenLogs { get; set; } = [];
    public List<OperationalApiClient.ListenLogDto> FilteredListenLogs { get; set; } = [];
    public List<OperationalApiClient.SystemLogDto> SystemLogs { get; set; } = [];
    public CreateSystemLogInput NewLog { get; set; } = new();
    public string Search { get; set; } = string.Empty;
    public int? PoiId { get; set; }
    public string? LanguageCode { get; set; }
    public string? ContentType { get; set; }
    public List<POI> Pois { get; set; } = [];
    public List<string> Languages { get; set; } = [];
}

public class CreateSystemLogInput
{
    public string Category { get; set; } = "system";
    public string Level { get; set; } = "info";
    public string Source { get; set; } = "admin";
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
}

public class DashboardToursViewModel
{
    public List<OperationalApiClient.TourSummaryDto> Tours { get; set; } = [];
    public List<POI> Pois { get; set; } = [];
    public OperationalApiClient.UpsertTourRequest NewTour { get; set; } = new();
    public int? SelectedTourId { get; set; }
    public OperationalApiClient.TourDetailDto? SelectedTour { get; set; }
    public string? StopsCsv { get; set; }
    public List<int> SelectedPoiIds { get; set; } = [];
}

public class DashboardTranslationsViewModel
{
    public List<OperationalApiClient.PoiTranslationDto> Translations { get; set; } = [];
    public List<POI> Pois { get; set; } = [];
    public OperationalApiClient.UpsertPoiTranslationRequest NewTranslation { get; set; } = new();
    public int? PoiId { get; set; }
    public string? LanguageCode { get; set; }
    public string? Status { get; set; }
    public string Search { get; set; } = string.Empty;
    public List<string> Languages { get; set; } = [];
}
