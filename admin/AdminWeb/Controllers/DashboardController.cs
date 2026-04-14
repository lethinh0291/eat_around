using AdminWeb.Models;
using AdminWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedLib.Models;

namespace AdminWeb.Controllers;

[Authorize(Roles = "admin")]
public class DashboardController : Controller
{
    private readonly PoiApiClient _poiApiClient;
    private readonly AdminManagementApiClient _adminManagementApiClient;
    private readonly OperationalApiClient _operationalApiClient;

    public DashboardController(
        PoiApiClient poiApiClient,
        AdminManagementApiClient adminManagementApiClient,
        OperationalApiClient operationalApiClient)
    {
        _poiApiClient = poiApiClient;
        _adminManagementApiClient = adminManagementApiClient;
        _operationalApiClient = operationalApiClient;
    }

    public async Task<IActionResult> Overview()
    {
        var summaryTask = BuildSummaryModelAsync();
        var toursTask = _operationalApiClient.GetToursAsync();
        var translationsTask = _operationalApiClient.GetPoiTranslationsAsync();
        var listenLogsTask = _operationalApiClient.GetListenLogsAsync();
        var systemLogsTask = _operationalApiClient.GetSystemLogsAsync();
        var poisTask = _poiApiClient.GetAllAsync();

        await Task.WhenAll(summaryTask, toursTask, translationsTask, listenLogsTask, systemLogsTask, poisTask);

        var listenLogs = listenLogsTask.Result;
        var todayStartUtc = DateTime.UtcNow.Date;
        var topPois = BuildTopListenPois(listenLogs, poisTask.Result, 5);
        var topLanguages = BuildTopListenLanguages(listenLogs, 8);

        var viewModel = new DashboardOverviewViewModel
        {
            Summary = summaryTask.Result,
            TotalTours = toursTask.Result.Count,
            TotalTranslations = translationsTask.Result.Count,
            TotalListenLogs = listenLogs.Count,
            TotalSystemLogs = systemLogsTask.Result.Count,
            ListenLogsLast7Days = listenLogs.Count(item => item.PlayedAtUtc >= DateTime.UtcNow.AddDays(-7)),
            AverageListenDurationSeconds = listenLogs.Count == 0 ? 0 : listenLogs.Average(item => item.DurationSeconds),
            AudioListenLogs = listenLogs.Count(item => string.Equals(item.ContentType, "audio", StringComparison.OrdinalIgnoreCase)),
            TtsListenLogs = listenLogs.Count(item => string.Equals(item.ContentType, "tts", StringComparison.OrdinalIgnoreCase)),
            TodayListenLogs = listenLogs.Count(item => item.PlayedAtUtc >= todayStartUtc),
            WeekListenLogs = listenLogs.Count(item => item.PlayedAtUtc >= DateTime.UtcNow.AddDays(-7)),
            DistinctListenLanguages = listenLogs
                .Select(item => string.IsNullOrWhiteSpace(item.LanguageCode) ? "vi" : item.LanguageCode.Trim().ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            ListenByDay = BuildListenByDay(listenLogs, 7),
            ListenByHour = BuildListenByHour(listenLogs),
            TopListenPois = topPois,
            TopListenLanguages = topLanguages,
            RecentActivities = BuildRecentActivities(listenLogs, systemLogsTask.Result, translationsTask.Result, toursTask.Result, poisTask.Result)
        };

        return View(viewModel);
    }

    public async Task<IActionResult> Analytics(DateTime? fromUtc, DateTime? toUtc, int? poiId, string? languageCode)
    {
        var from = fromUtc ?? DateTime.UtcNow.AddDays(-7);
        var to = toUtc ?? DateTime.UtcNow;

        var listenLogsTask = _operationalApiClient.GetListenLogsAsync(from, to);
        var poisTask = _poiApiClient.GetAllAsync();

        await Task.WhenAll(listenLogsTask, poisTask);

        var normalizedLanguage = string.IsNullOrWhiteSpace(languageCode)
            ? null
            : languageCode.Trim().ToLowerInvariant();
        var filteredListenLogs = listenLogsTask.Result
            .Where(item => !poiId.HasValue || item.PoiId == poiId.Value)
            .Where(item => string.IsNullOrWhiteSpace(normalizedLanguage) ||
                           string.Equals((item.LanguageCode ?? string.Empty).Trim(), normalizedLanguage, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.PlayedAtUtc)
            .ToList();

        var topPois = BuildTopPoisFromLogs(filteredListenLogs, poisTask.Result);
        var languageRatios = BuildLanguageRatiosFromLogs(filteredListenLogs);
        var heatmapPoints = BuildHeatmapFromLogs(filteredListenLogs);
        var languages = listenLogsTask.Result
            .Select(item => string.IsNullOrWhiteSpace(item.LanguageCode) ? "vi" : item.LanguageCode.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item)
            .ToList();

        var viewModel = new DashboardAnalyticsViewModel
        {
            FromUtc = from,
            ToUtc = to,
            PoiId = poiId,
            LanguageCode = normalizedLanguage,
            ListenLogs = filteredListenLogs,
            HeatmapPoints = heatmapPoints,
            TopPois = topPois,
            LanguageRatios = languageRatios,
            Pois = poisTask.Result.OrderBy(item => item.Name).ToList(),
            Languages = languages
        };

        return View(viewModel);
    }

    public async Task<IActionResult> Logs(string? search, int? poiId, string? languageCode, string? contentType)
    {
        var from = DateTime.UtcNow.AddDays(-30);
        var to = DateTime.UtcNow;

        var listenLogsTask = _operationalApiClient.GetListenLogsAsync(from, to);
        var systemLogsTask = _operationalApiClient.GetSystemLogsAsync(from, to);
        var poisTask = _poiApiClient.GetAllAsync();

        await Task.WhenAll(listenLogsTask, systemLogsTask, poisTask);

        var filtered = FilterListenLogs(listenLogsTask.Result, search, poiId, languageCode, contentType);
        var languages = listenLogsTask.Result
            .Select(item => string.IsNullOrWhiteSpace(item.LanguageCode) ? "vi" : item.LanguageCode.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item)
            .ToList();

        return View(new DashboardLogsViewModel
        {
            ListenLogs = listenLogsTask.Result,
            FilteredListenLogs = filtered,
            SystemLogs = systemLogsTask.Result.OrderByDescending(item => item.CreatedAtUtc).ToList(),
            NewLog = new CreateSystemLogInput(),
            Search = search?.Trim() ?? string.Empty,
            PoiId = poiId,
            LanguageCode = languageCode,
            ContentType = contentType,
            Pois = poisTask.Result.OrderBy(item => item.Name).ToList(),
            Languages = languages
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSystemLog(CreateSystemLogInput input)
    {
        var request = new OperationalApiClient.CreateSystemLogRequest
        {
            Category = input.Category,
            Level = input.Level,
            Source = input.Source,
            Message = input.Message,
            Details = input.Details
        };

        var result = await _operationalApiClient.CreateSystemLogAsync(request);
        TempData[result.Success ? "AdminMessage" : "AdminError"] = result.Message;
        return RedirectToAction(nameof(Logs));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSystemLog(int id)
    {
        var result = await _operationalApiClient.DeleteSystemLogAsync(id);
        TempData[result.Success ? "AdminMessage" : "AdminError"] = result.Message;
        return RedirectToAction(nameof(Logs));
    }

    public async Task<IActionResult> Tours(int? selectedTourId)
    {
        var toursTask = _operationalApiClient.GetToursAsync();
        var poisTask = _poiApiClient.GetAllAsync();
        await Task.WhenAll(toursTask, poisTask);

        OperationalApiClient.TourDetailDto? selectedTour = null;
        string? stopsCsv = null;

        if (selectedTourId.HasValue)
        {
            selectedTour = await _operationalApiClient.GetTourByIdAsync(selectedTourId.Value);
            if (selectedTour is not null)
            {
                stopsCsv = string.Join(",", selectedTour.Stops.OrderBy(item => item.SortOrder).Select(item => item.PoiId));
            }
        }

        var selectedPoiIds = selectedTour?.Stops
            .OrderBy(item => item.SortOrder)
            .Select(item => item.PoiId)
            .ToList() ?? [];

        return View(new DashboardToursViewModel
        {
            Tours = toursTask.Result,
            Pois = poisTask.Result.OrderByDescending(item => item.Priority).ThenBy(item => item.Name).ToList(),
            SelectedTourId = selectedTourId,
            SelectedTour = selectedTour,
            StopsCsv = stopsCsv,
            SelectedPoiIds = selectedPoiIds,
            NewTour = new OperationalApiClient.UpsertTourRequest { IsActive = true }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTour(OperationalApiClient.UpsertTourRequest request, List<int>? poiIds)
    {
        var result = await _operationalApiClient.CreateTourAsync(request);

        if (result.Success && poiIds is { Count: > 0 })
        {
            var tours = await _operationalApiClient.GetToursAsync();
            var newTour = tours
                .Where(item => string.Equals(item.Name, request.Name, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.CreatedAtUtc)
                .FirstOrDefault();

            if (newTour is not null)
            {
                var replaceRequest = new OperationalApiClient.ReplaceTourStopsRequest
                {
                    Stops = poiIds
                        .Where(item => item > 0)
                        .Distinct()
                        .Select((poiId, index) => new OperationalApiClient.TourStopRequest
                        {
                            PoiId = poiId,
                            SortOrder = index + 1
                        })
                        .ToList()
                };

                var saveStopsResult = await _operationalApiClient.ReplaceTourStopsAsync(newTour.Id, replaceRequest);
                if (!saveStopsResult.Success)
                {
                    TempData["AdminError"] = saveStopsResult.Message;
                }
            }
        }

        TempData[result.Success ? "AdminMessage" : "AdminError"] = result.Message;
        return RedirectToAction(nameof(Tours));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTour(int id, OperationalApiClient.UpsertTourRequest request)
    {
        var result = await _operationalApiClient.UpdateTourAsync(id, request);
        TempData[result.Success ? "AdminMessage" : "AdminError"] = result.Message;
        return RedirectToAction(nameof(Tours), new { selectedTourId = id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTour(int id)
    {
        var result = await _operationalApiClient.DeleteTourAsync(id);
        TempData[result.Success ? "AdminMessage" : "AdminError"] = result.Message;
        return RedirectToAction(nameof(Tours));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTourStops(int id, string? stopsCsv, List<int>? poiIds)
    {
        var orderedPoiIds = poiIds is { Count: > 0 }
            ? poiIds.Where(item => item > 0).Distinct().ToList()
            : ParsePoiIds(stopsCsv);

        var replaceRequest = new OperationalApiClient.ReplaceTourStopsRequest
        {
            Stops = orderedPoiIds
                .Select((poiId, index) => new OperationalApiClient.TourStopRequest
                {
                    PoiId = poiId,
                    SortOrder = index + 1
                })
                .ToList()
        };

        var result = await _operationalApiClient.ReplaceTourStopsAsync(id, replaceRequest);
        TempData[result.Success ? "AdminMessage" : "AdminError"] = result.Message;
        return RedirectToAction(nameof(Tours), new { selectedTourId = id });
    }

    public async Task<IActionResult> Translations(int? poiId, string? languageCode, string? status, string? search)
    {
        var translationsTask = _operationalApiClient.GetPoiTranslationsAsync(poiId, languageCode, status);
        var poisTask = _poiApiClient.GetAllAsync();
        await Task.WhenAll(translationsTask, poisTask);

        var filtered = translationsTask.Result;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            filtered = filtered
                .Where(item =>
                    item.PoiName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    (item.Title ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    item.ContentText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var languages = translationsTask.Result
            .Select(item => string.IsNullOrWhiteSpace(item.LanguageCode) ? "vi" : item.LanguageCode.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item)
            .ToList();

        return View(new DashboardTranslationsViewModel
        {
            Translations = filtered.OrderByDescending(item => item.SubmittedAtUtc).ToList(),
            Pois = poisTask.Result.OrderBy(item => item.Name).ToList(),
            NewTranslation = new OperationalApiClient.UpsertPoiTranslationRequest
            {
                LanguageCode = "vi"
            },
            PoiId = poiId,
            LanguageCode = languageCode,
            Status = status,
            Search = search?.Trim() ?? string.Empty,
            Languages = languages
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTranslation(OperationalApiClient.UpsertPoiTranslationRequest request)
    {
        request.SubmittedBy = User.Identity?.Name ?? "admin";
        var result = await _operationalApiClient.CreatePoiTranslationAsync(request);
        TempData[result.Success ? "AdminMessage" : "AdminError"] = result.Message;
        return RedirectToAction(nameof(Translations));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTranslation(int id, OperationalApiClient.UpsertPoiTranslationRequest request)
    {
        var result = await _operationalApiClient.UpdatePoiTranslationAsync(id, request);
        TempData[result.Success ? "AdminMessage" : "AdminError"] = result.Message;
        return RedirectToAction(nameof(Translations));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTranslationStatus(int id, string status)
    {
        var request = new OperationalApiClient.UpdatePoiTranslationStatusRequest
        {
            Status = status,
            ReviewedBy = User.Identity?.Name ?? "admin"
        };

        var result = await _operationalApiClient.UpdatePoiTranslationStatusAsync(id, request);
        TempData[result.Success ? "AdminMessage" : "AdminError"] = result.Message;
        return RedirectToAction(nameof(Translations));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTranslation(int id)
    {
        var result = await _operationalApiClient.DeletePoiTranslationAsync(id);
        TempData[result.Success ? "AdminMessage" : "AdminError"] = result.Message;
        return RedirectToAction(nameof(Translations));
    }

    private async Task<AdminDashboardViewModel> BuildSummaryModelAsync()
    {
        var poisTask = _poiApiClient.GetAllAsync();
        var usersTask = _adminManagementApiClient.GetUsersAsync();
        var registrationsTask = _adminManagementApiClient.GetStoreRegistrationsAsync();

        await Task.WhenAll(poisTask, usersTask, registrationsTask);

        var pois = poisTask.Result;
        var users = usersTask.Result;
        var registrations = registrationsTask.Result;
        var orderedPois = pois.OrderByDescending(poi => poi.Priority).ThenBy(poi => poi.Name).ToList();

        var sellerUsers = users.Count(user => string.Equals(user.Role, "seller", StringComparison.OrdinalIgnoreCase));
        var adminUsers = users.Count(user => string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase));
        var distinctLanguages = orderedPois
            .Select(poi => poi.LanguageCode)
            .Where(language => !string.IsNullOrWhiteSpace(language))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var mediaPois = orderedPois.Count(poi => !string.IsNullOrWhiteSpace(poi.ImageUrl) || !string.IsNullOrWhiteSpace(poi.AudioUrl));

        return new AdminDashboardViewModel
        {
            TotalPois = orderedPois.Count,
            TotalUsers = users.Count,
            SellerUsers = sellerUsers,
            AdminUsers = adminUsers,
            PendingStoreRegistrations = registrations.Count,
            HighPriorityPois = orderedPois.Count(poi => poi.Priority >= 7),
            AverageRadius = orderedPois.Count == 0 ? 0 : orderedPois.Average(poi => poi.Radius),
            AveragePriority = orderedPois.Count == 0 ? 0 : orderedPois.Average(poi => poi.Priority),
            DistinctLanguages = distinctLanguages,
            MediaPois = mediaPois,
            FeaturedPois = orderedPois.Take(5).ToList(),
            MetricCards =
            [
                new DashboardMetricCardViewModel
                {
                    Title = "Tổng POI",
                    Value = orderedPois.Count.ToString(),
                    Note = "Dữ liệu đang được mobile app sử dụng",
                    Tone = "accent",
                    Icon = "poi",
                    ActionText = "Mở POI",
                    ActionUrl = "/Poi"
                },
                new DashboardMetricCardViewModel
                {
                    Title = "Tài khoản",
                    Value = users.Count.ToString(),
                    Note = $"{sellerUsers} chủ quán, còn lại là admin và khách",
                    Tone = "neutral",
                    Icon = "users",
                    ActionText = "Xem user",
                    ActionUrl = "/AdminManagement/Users"
                },
                new DashboardMetricCardViewModel
                {
                    Title = "Ngôn ngữ hỗ trợ",
                    Value = distinctLanguages.ToString(),
                    Note = "Theo mã ngôn ngữ hiện có trong POI",
                    Tone = "warm",
                    Icon = "globe",
                    ActionText = "Quản lý",
                    ActionUrl = "/Dashboard/Translations"
                },
                new DashboardMetricCardViewModel
                {
                    Title = "Đăng ký chờ duyệt",
                    Value = registrations.Count.ToString(),
                    Note = "Nguồn cho luồng gán chủ quán",
                    Tone = "soft",
                    Icon = "inbox",
                    ActionText = "Mở Stores",
                    ActionUrl = "/AdminManagement/Stores"
                },
                new DashboardMetricCardViewModel
                {
                    Title = "POI có media",
                    Value = mediaPois.ToString(),
                    Note = "Có ảnh hoặc audio đi kèm",
                    Tone = "success",
                    Icon = "media",
                    ActionText = "Xem chi tiết",
                    ActionUrl = "/Poi/Index"
                }
            ]
        };
    }

    private static List<int> ParsePoiIds(string? stopsCsv)
    {
        if (string.IsNullOrWhiteSpace(stopsCsv))
        {
            return [];
        }

        return stopsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out var number) ? number : 0)
            .Where(value => value > 0)
            .Distinct()
            .ToList();
    }

    private static List<DashboardListenPoiViewModel> BuildTopListenPois(
        List<OperationalApiClient.ListenLogDto> listenLogs,
        List<POI> pois,
        int take)
    {
        if (listenLogs.Count == 0)
        {
            return [];
        }

        var topPoiIds = listenLogs
            .GroupBy(item => item.PoiId)
            .Select(group => new { PoiId = group.Key, Listens = group.Count() })
            .OrderByDescending(item => item.Listens)
            .Take(take)
            .ToList();

        var poiNames = pois.ToDictionary(item => item.Id, item => item.Name);
        var totalListens = topPoiIds.Sum(item => item.Listens);

        return topPoiIds
            .Select(item => new DashboardListenPoiViewModel
            {
                PoiId = item.PoiId,
                PoiName = poiNames.TryGetValue(item.PoiId, out var name) ? name : $"POI {item.PoiId}",
                Listens = item.Listens,
                Percent = totalListens == 0 ? 0 : Math.Round((item.Listens * 100d) / totalListens, 2)
            })
            .ToList();
    }

    private static List<DashboardLanguageShareViewModel> BuildTopListenLanguages(
        List<OperationalApiClient.ListenLogDto> listenLogs,
        int take)
    {
        if (listenLogs.Count == 0)
        {
            return [];
        }

        var grouped = listenLogs
            .GroupBy(item => string.IsNullOrWhiteSpace(item.LanguageCode) ? "vi" : item.LanguageCode.Trim().ToLowerInvariant())
            .Select(group => new DashboardLanguageShareViewModel
            {
                LanguageCode = group.Key,
                Listens = group.Count()
            })
            .OrderByDescending(item => item.Listens)
            .Take(take)
            .ToList();

        var total = grouped.Sum(item => item.Listens);
        foreach (var item in grouped)
        {
            item.Percent = total == 0 ? 0 : Math.Round((item.Listens * 100.0) / total, 2);
        }

        return grouped;
    }

    private static List<DashboardTimeCountViewModel> BuildListenByDay(List<OperationalApiClient.ListenLogDto> listenLogs, int days)
    {
        var today = DateTime.UtcNow.Date;
        var counts = listenLogs
            .GroupBy(item => item.PlayedAtUtc.Date)
            .ToDictionary(group => group.Key, group => group.Count());

        var output = new List<DashboardTimeCountViewModel>(days);
        for (var index = days - 1; index >= 0; index--)
        {
            var date = today.AddDays(-index);
            output.Add(new DashboardTimeCountViewModel
            {
                Label = date.ToString("dd/MM"),
                Count = counts.TryGetValue(date, out var count) ? count : 0
            });
        }

        return output;
    }

    private static List<DashboardTimeCountViewModel> BuildListenByHour(List<OperationalApiClient.ListenLogDto> listenLogs)
    {
        var hourly = listenLogs
            .GroupBy(item => item.PlayedAtUtc.Hour)
            .ToDictionary(group => group.Key, group => group.Count());

        var output = new List<DashboardTimeCountViewModel>(24);
        for (var hour = 0; hour <= 23; hour++)
        {
            output.Add(new DashboardTimeCountViewModel
            {
                Label = $"{hour:00}h",
                Count = hourly.TryGetValue(hour, out var count) ? count : 0
            });
        }

        return output;
    }

    private static List<DashboardRecentActivityViewModel> BuildRecentActivities(
        List<OperationalApiClient.ListenLogDto> listenLogs,
        List<OperationalApiClient.SystemLogDto> systemLogs,
        List<OperationalApiClient.PoiTranslationDto> translations,
        List<OperationalApiClient.TourSummaryDto> tours,
        List<POI> pois)
    {
        var poiNames = pois.ToDictionary(item => item.Id, item => item.Name);
        var activities = new List<(DateTime Time, DashboardRecentActivityViewModel Item)>();

        foreach (var log in systemLogs.OrderByDescending(item => item.CreatedAtUtc).Take(4))
        {
            activities.Add((log.CreatedAtUtc, new DashboardRecentActivityViewModel
            {
                TimeLabel = log.CreatedAtUtc.ToLocalTime().ToString("dd/MM HH:mm"),
                Title = $"[{log.Level}] {log.Source}",
                Description = log.Message,
                Tone = string.Equals(log.Level, "error", StringComparison.OrdinalIgnoreCase) ? "danger" : "neutral"
            }));
        }

        foreach (var translation in translations.OrderByDescending(item => item.SubmittedAtUtc).Take(3))
        {
            activities.Add((translation.SubmittedAtUtc, new DashboardRecentActivityViewModel
            {
                TimeLabel = translation.SubmittedAtUtc.ToLocalTime().ToString("dd/MM HH:mm"),
                Title = $"Bản dịch {translation.LanguageCode?.ToUpperInvariant()}",
                Description = $"{translation.PoiName} - {translation.Status}",
                Tone = string.Equals(translation.Status, "published", StringComparison.OrdinalIgnoreCase) ? "success" : "warning"
            }));
        }

        foreach (var tour in tours.OrderByDescending(item => item.UpdatedAtUtc).Take(3))
        {
            activities.Add((tour.UpdatedAtUtc, new DashboardRecentActivityViewModel
            {
                TimeLabel = tour.UpdatedAtUtc.ToLocalTime().ToString("dd/MM HH:mm"),
                Title = "Tour cập nhật",
                Description = $"{tour.Name} - {tour.StopCount} điểm dừng",
                Tone = "accent"
            }));
        }

        foreach (var listen in listenLogs.OrderByDescending(item => item.PlayedAtUtc).Take(4))
        {
            var poiName = poiNames.TryGetValue(listen.PoiId, out var name) ? name : $"POI {listen.PoiId}";
            activities.Add((listen.PlayedAtUtc, new DashboardRecentActivityViewModel
            {
                TimeLabel = listen.PlayedAtUtc.ToLocalTime().ToString("dd/MM HH:mm"),
                Title = "Lượt nghe mới",
                Description = $"{poiName} - {listen.LanguageCode} ({listen.ContentType})",
                Tone = "soft"
            }));
        }

        return activities
            .OrderByDescending(item => item.Time)
            .Take(10)
            .Select(item => item.Item)
            .ToList();
    }

    private static List<OperationalApiClient.TopPoiListenDto> BuildTopPoisFromLogs(
        List<OperationalApiClient.ListenLogDto> listenLogs,
        List<POI> pois)
    {
        if (listenLogs.Count == 0)
        {
            return [];
        }

        var names = pois.ToDictionary(item => item.Id, item => item.Name);
        var grouped = listenLogs
            .GroupBy(item => item.PoiId)
            .Select(group => new { PoiId = group.Key, Listens = group.Count() })
            .OrderByDescending(item => item.Listens)
            .Take(10)
            .ToList();

        var total = grouped.Sum(item => item.Listens);
        return grouped
            .Select(item => new OperationalApiClient.TopPoiListenDto
            {
                PoiId = item.PoiId,
                PoiName = names.TryGetValue(item.PoiId, out var name) ? name : $"POI {item.PoiId}",
                Listens = item.Listens,
                Percent = total == 0 ? 0 : Math.Round((item.Listens * 100d) / total, 2)
            })
            .ToList();
    }

    private static List<OperationalApiClient.LanguageRatioDto> BuildLanguageRatiosFromLogs(
        List<OperationalApiClient.ListenLogDto> listenLogs)
    {
        if (listenLogs.Count == 0)
        {
            return [];
        }

        var grouped = listenLogs
            .GroupBy(item => string.IsNullOrWhiteSpace(item.LanguageCode) ? "vi" : item.LanguageCode.Trim().ToLowerInvariant())
            .Select(group => new { LanguageCode = group.Key, Listens = group.Count() })
            .OrderByDescending(item => item.Listens)
            .Take(10)
            .ToList();

        var total = grouped.Sum(item => item.Listens);
        return grouped
            .Select(item => new OperationalApiClient.LanguageRatioDto
            {
                LanguageCode = item.LanguageCode,
                Listens = item.Listens,
                Percent = total == 0 ? 0 : Math.Round((item.Listens * 100d) / total, 2)
            })
            .ToList();
    }

    private static List<OperationalApiClient.HeatmapPointDto> BuildHeatmapFromLogs(
        List<OperationalApiClient.ListenLogDto> listenLogs)
    {
        return listenLogs
            .Where(item => Math.Abs(item.Latitude) > 0.001 || Math.Abs(item.Longitude) > 0.001)
            .GroupBy(item => new
            {
                Latitude = Math.Round(item.Latitude, 3),
                Longitude = Math.Round(item.Longitude, 3)
            })
            .Select(group => new OperationalApiClient.HeatmapPointDto
            {
                Latitude = group.Key.Latitude,
                Longitude = group.Key.Longitude,
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .Take(120)
            .ToList();
    }

    private static List<OperationalApiClient.ListenLogDto> FilterListenLogs(
        List<OperationalApiClient.ListenLogDto> listenLogs,
        string? search,
        int? poiId,
        string? languageCode,
        string? contentType)
    {
        var keyword = search?.Trim() ?? string.Empty;
        var filtered = listenLogs
            .Where(item => !poiId.HasValue || item.PoiId == poiId.Value)
            .Where(item => string.IsNullOrWhiteSpace(languageCode) ||
                           string.Equals((item.LanguageCode ?? string.Empty).Trim(), languageCode.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrWhiteSpace(contentType) ||
                           string.Equals((item.ContentType ?? string.Empty).Trim(), contentType.Trim(), StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            filtered = filtered.Where(item =>
                item.DeviceId.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                item.PoiId.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                item.LanguageCode.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                item.ContentType.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        return filtered
            .OrderByDescending(item => item.PlayedAtUtc)
            .Take(500)
            .ToList();
    }
}
