using BackendAPI.Data;
using BackendAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendAPI.Controllers;

[ApiController]
[Route("api/poi-translations")]
public class PoiTranslationsController : ControllerBase
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "pending",
        "approved",
        "rejected"
    };

    private readonly AppDbContext _dbContext;

    public PoiTranslationsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? poiId, [FromQuery] string? languageCode, [FromQuery] string? status)
    {
        var query = _dbContext.PoiTranslations.AsNoTracking().AsQueryable();

        if (poiId.HasValue)
        {
            query = query.Where(item => item.PoiId == poiId.Value);
        }

        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            var normalizedLanguage = languageCode.Trim().ToLowerInvariant();
            query = query.Where(item => item.LanguageCode.ToLower() == normalizedLanguage);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToLowerInvariant();
            query = query.Where(item => item.Status.ToLower() == normalizedStatus);
        }

        var items = await query
            .OrderByDescending(item => item.SubmittedAtUtc)
            .Take(1500)
            .ToListAsync();

        var poiNames = await _dbContext.POIs
            .AsNoTracking()
            .Where(poi => items.Select(item => item.PoiId).Contains(poi.Id))
            .ToDictionaryAsync(poi => poi.Id, poi => poi.Name);

        var result = items.Select(item => new
        {
            item.Id,
            item.PoiId,
            poiName = poiNames.TryGetValue(item.PoiId, out var name) ? name : $"POI {item.PoiId}",
            item.LanguageCode,
            item.Title,
            item.ContentText,
            item.AudioUrl,
            item.Status,
            item.SubmittedBy,
            item.ReviewedBy,
            item.SubmittedAtUtc,
            item.ReviewedAtUtc
        });

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertPoiTranslationRequest request)
    {
        if (request.PoiId <= 0 || !await _dbContext.POIs.AnyAsync(item => item.Id == request.PoiId))
        {
            return BadRequest(new { message = "POI không hợp lệ." });
        }

        if (string.IsNullOrWhiteSpace(request.ContentText))
        {
            return BadRequest(new { message = "Nội dung dịch là bắt buộc." });
        }

        var model = new PoiTranslation
        {
            PoiId = request.PoiId,
            LanguageCode = string.IsNullOrWhiteSpace(request.LanguageCode) ? "vi" : request.LanguageCode.Trim().ToLowerInvariant(),
            Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
            ContentText = request.ContentText.Trim(),
            AudioUrl = string.IsNullOrWhiteSpace(request.AudioUrl) ? null : request.AudioUrl.Trim(),
            Status = "pending",
            SubmittedBy = string.IsNullOrWhiteSpace(request.SubmittedBy) ? "admin" : request.SubmittedBy.Trim(),
            SubmittedAtUtc = DateTime.UtcNow
        };

        _dbContext.PoiTranslations.Add(model);
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Đã tạo bản dịch mới.", model.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertPoiTranslationRequest request)
    {
        var model = await _dbContext.PoiTranslations.FirstOrDefaultAsync(item => item.Id == id);
        if (model is null)
        {
            return NotFound(new { message = "Không tìm thấy bản dịch." });
        }

        if (request.PoiId <= 0 || !await _dbContext.POIs.AnyAsync(item => item.Id == request.PoiId))
        {
            return BadRequest(new { message = "POI không hợp lệ." });
        }

        if (string.IsNullOrWhiteSpace(request.ContentText))
        {
            return BadRequest(new { message = "Nội dung dịch là bắt buộc." });
        }

        model.PoiId = request.PoiId;
        model.LanguageCode = string.IsNullOrWhiteSpace(request.LanguageCode) ? "vi" : request.LanguageCode.Trim().ToLowerInvariant();
        model.Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();
        model.ContentText = request.ContentText.Trim();
        model.AudioUrl = string.IsNullOrWhiteSpace(request.AudioUrl) ? null : request.AudioUrl.Trim();

        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "Đã cập nhật bản dịch." });
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateTranslationStatusRequest request)
    {
        var model = await _dbContext.PoiTranslations.FirstOrDefaultAsync(item => item.Id == id);
        if (model is null)
        {
            return NotFound(new { message = "Không tìm thấy bản dịch." });
        }

        var normalizedStatus = (request.Status ?? string.Empty).Trim().ToLowerInvariant();
        if (!AllowedStatuses.Contains(normalizedStatus))
        {
            return BadRequest(new { message = "Trạng thái bản dịch không hợp lệ." });
        }

        model.Status = normalizedStatus;
        model.ReviewedBy = string.IsNullOrWhiteSpace(request.ReviewedBy) ? "admin" : request.ReviewedBy.Trim();
        model.ReviewedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "Đã cập nhật trạng thái bản dịch." });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var model = await _dbContext.PoiTranslations.FirstOrDefaultAsync(item => item.Id == id);
        if (model is null)
        {
            return NotFound(new { message = "Không tìm thấy bản dịch." });
        }

        _dbContext.PoiTranslations.Remove(model);
        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "Đã xóa bản dịch." });
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

    public sealed class UpdateTranslationStatusRequest
    {
        public string Status { get; set; } = string.Empty;
        public string? ReviewedBy { get; set; }
    }
}
