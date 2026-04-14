using BackendAPI.Data;
using BackendAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendAPI.Controllers;

[ApiController]
[Route("api/listen-logs")]
public class ListenLogsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public ListenLogsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ListenLog>>> Get(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int? poiId,
        [FromQuery] string? languageCode)
    {
        var query = _dbContext.ListenLogs.AsNoTracking().AsQueryable();

        if (fromUtc.HasValue)
        {
            query = query.Where(item => item.PlayedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(item => item.PlayedAtUtc <= toUtc.Value);
        }

        if (poiId.HasValue)
        {
            query = query.Where(item => item.PoiId == poiId.Value);
        }

        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            var normalizedLanguage = languageCode.Trim().ToLowerInvariant();
            query = query.Where(item => item.LanguageCode.ToLower() == normalizedLanguage);
        }

        var result = await query
            .OrderByDescending(item => item.PlayedAtUtc)
            .Take(1500)
            .ToListAsync();

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateListenLogRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            return BadRequest(new { message = "DeviceId là bắt buộc." });
        }

        if (!await _dbContext.POIs.AnyAsync(item => item.Id == request.PoiId))
        {
            return BadRequest(new { message = "PoiId không hợp lệ." });
        }

        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
        {
            return BadRequest(new { message = "Tọa độ không hợp lệ." });
        }

        var model = new ListenLog
        {
            DeviceId = request.DeviceId.Trim(),
            PoiId = request.PoiId,
            LanguageCode = string.IsNullOrWhiteSpace(request.LanguageCode) ? "vi" : request.LanguageCode.Trim().ToLowerInvariant(),
            ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "tts" : request.ContentType.Trim().ToLowerInvariant(),
            DurationSeconds = Math.Max(0, request.DurationSeconds),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            PlayedAtUtc = request.PlayedAtUtc ?? DateTime.UtcNow
        };

        _dbContext.ListenLogs.Add(model);
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Đã ghi nhận listen log.", model.Id });
    }

    [HttpGet("heatmap")]
    public async Task<IActionResult> Heatmap(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int precision = 3)
    {
        var bucket = Math.Clamp(precision, 2, 4);
        var query = _dbContext.ListenLogs.AsNoTracking().AsQueryable();

        if (fromUtc.HasValue)
        {
            query = query.Where(item => item.PlayedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(item => item.PlayedAtUtc <= toUtc.Value);
        }

        var logs = await query.ToListAsync();

        var points = logs
            .GroupBy(item => new
            {
                Lat = Math.Round(item.Latitude, bucket),
                Lng = Math.Round(item.Longitude, bucket)
            })
            .Select(group => new
            {
                latitude = group.Key.Lat,
                longitude = group.Key.Lng,
                count = group.Count()
            })
            .OrderByDescending(item => item.count)
            .Take(200)
            .ToList();

        return Ok(points);
    }

    [HttpGet("top-pois")]
    public async Task<IActionResult> TopPois([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc)
    {
        var query = _dbContext.ListenLogs.AsNoTracking().AsQueryable();

        if (fromUtc.HasValue)
        {
            query = query.Where(item => item.PlayedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(item => item.PlayedAtUtc <= toUtc.Value);
        }

        var grouped = await query
            .GroupBy(item => item.PoiId)
            .Select(group => new
            {
                poiId = group.Key,
                listens = group.Count()
            })
            .OrderByDescending(item => item.listens)
            .Take(5)
            .ToListAsync();

        var poiNames = await _dbContext.POIs
            .AsNoTracking()
            .Where(item => grouped.Select(g => g.poiId).Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name);

        var total = grouped.Sum(item => item.listens);
        var result = grouped.Select(item => new
        {
            item.poiId,
            poiName = poiNames.TryGetValue(item.poiId, out var name) ? name : $"POI {item.poiId}",
            item.listens,
            percent = total == 0 ? 0 : Math.Round((item.listens * 100.0) / total, 2)
        });

        return Ok(result);
    }

    [HttpGet("language-ratio")]
    public async Task<IActionResult> LanguageRatio([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc)
    {
        var query = _dbContext.ListenLogs.AsNoTracking().AsQueryable();

        if (fromUtc.HasValue)
        {
            query = query.Where(item => item.PlayedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(item => item.PlayedAtUtc <= toUtc.Value);
        }

        var grouped = await query
            .GroupBy(item => item.LanguageCode)
            .Select(group => new
            {
                languageCode = group.Key,
                listens = group.Count()
            })
            .OrderByDescending(item => item.listens)
            .ToListAsync();

        var total = grouped.Sum(item => item.listens);
        var result = grouped.Select(item => new
        {
            item.languageCode,
            item.listens,
            percent = total == 0 ? 0 : Math.Round((item.listens * 100.0) / total, 2)
        });

        return Ok(result);
    }

    public sealed class CreateListenLogRequest
    {
        public string DeviceId { get; set; } = string.Empty;
        public int PoiId { get; set; }
        public string? LanguageCode { get; set; }
        public string? ContentType { get; set; }
        public double DurationSeconds { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime? PlayedAtUtc { get; set; }
    }
}
