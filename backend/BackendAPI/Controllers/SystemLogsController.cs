using BackendAPI.Data;
using BackendAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendAPI.Controllers;

[ApiController]
[Route("api/system-logs")]
public class SystemLogsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public SystemLogsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SystemLogEntry>>> Get(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? category,
        [FromQuery] string? level)
    {
        var query = _dbContext.SystemLogEntries.AsNoTracking().AsQueryable();

        if (fromUtc.HasValue)
        {
            query = query.Where(item => item.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(item => item.CreatedAtUtc <= toUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalizedCategory = category.Trim().ToLowerInvariant();
            query = query.Where(item => item.Category.ToLower() == normalizedCategory);
        }

        if (!string.IsNullOrWhiteSpace(level))
        {
            var normalizedLevel = level.Trim().ToLowerInvariant();
            query = query.Where(item => item.Level.ToLower() == normalizedLevel);
        }

        var result = await query
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(1500)
            .ToListAsync();

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSystemLogRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "Message là bắt buộc." });
        }

        var entry = new SystemLogEntry
        {
            Category = string.IsNullOrWhiteSpace(request.Category) ? "system" : request.Category.Trim().ToLowerInvariant(),
            Level = string.IsNullOrWhiteSpace(request.Level) ? "info" : request.Level.Trim().ToLowerInvariant(),
            Source = string.IsNullOrWhiteSpace(request.Source) ? "backend" : request.Source.Trim(),
            Message = request.Message.Trim(),
            Details = string.IsNullOrWhiteSpace(request.Details) ? null : request.Details.Trim(),
            CreatedAtUtc = request.CreatedAtUtc ?? DateTime.UtcNow
        };

        _dbContext.SystemLogEntries.Add(entry);
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Đã ghi log hệ thống.", entry.Id });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _dbContext.SystemLogEntries.FindAsync(id);
        if (item is null)
        {
            return NotFound(new { message = "Không tìm thấy log hệ thống." });
        }

        _dbContext.SystemLogEntries.Remove(item);
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Đã xóa log hệ thống." });
    }

    [HttpDelete("cleanup")]
    public async Task<IActionResult> Cleanup([FromQuery] int keepLatest = 1000)
    {
        var keep = Math.Max(100, keepLatest);
        var idsToKeep = await _dbContext.SystemLogEntries
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(keep)
            .Select(item => item.Id)
            .ToListAsync();

        var staleEntries = await _dbContext.SystemLogEntries
            .Where(item => !idsToKeep.Contains(item.Id))
            .ToListAsync();

        if (staleEntries.Count == 0)
        {
            return Ok(new { message = "Không có log cũ cần dọn.", deleted = 0 });
        }

        _dbContext.SystemLogEntries.RemoveRange(staleEntries);
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Đã dọn log cũ.", deleted = staleEntries.Count });
    }

    public sealed class CreateSystemLogRequest
    {
        public string? Category { get; set; }
        public string? Level { get; set; }
        public string? Source { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime? CreatedAtUtc { get; set; }
    }
}
