using BackendAPI.Data;
using BackendAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendAPI.Controllers;

[ApiController]
[Route("api/tours")]
public class ToursController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public ToursController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tours = await _dbContext.Tours
            .AsNoTracking()
            .Include(tour => tour.Stops)
            .OrderByDescending(tour => tour.UpdatedAtUtc)
            .Select(tour => new
            {
                tour.Id,
                tour.Name,
                tour.Description,
                tour.CoverImageUrl,
                tour.IsActive,
                stopCount = tour.Stops.Count,
                tour.CreatedAtUtc,
                tour.UpdatedAtUtc
            })
            .ToListAsync();

        return Ok(tours);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var tour = await _dbContext.Tours
            .AsNoTracking()
            .Include(item => item.Stops)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (tour is null)
        {
            return NotFound(new { message = "Không tìm thấy tour." });
        }

        var poiNames = await _dbContext.POIs
            .AsNoTracking()
            .Where(poi => tour.Stops.Select(stop => stop.PoiId).Contains(poi.Id))
            .ToDictionaryAsync(poi => poi.Id, poi => poi.Name);

        return Ok(new
        {
            tour.Id,
            tour.Name,
            tour.Description,
            tour.CoverImageUrl,
            tour.IsActive,
            tour.CreatedAtUtc,
            tour.UpdatedAtUtc,
            stops = tour.Stops
                .OrderBy(stop => stop.SortOrder)
                .Select(stop => new
                {
                    stop.Id,
                    stop.PoiId,
                    poiName = poiNames.TryGetValue(stop.PoiId, out var name) ? name : $"POI {stop.PoiId}",
                    stop.SortOrder,
                    stop.Note
                })
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertTourRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Tên tour là bắt buộc." });
        }

        var tour = new Tour
        {
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CoverImageUrl = string.IsNullOrWhiteSpace(request.CoverImageUrl) ? null : request.CoverImageUrl.Trim(),
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Tours.Add(tour);
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Đã tạo tour.", tour.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertTourRequest request)
    {
        var tour = await _dbContext.Tours.FirstOrDefaultAsync(item => item.Id == id);
        if (tour is null)
        {
            return NotFound(new { message = "Không tìm thấy tour." });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Tên tour là bắt buộc." });
        }

        tour.Name = request.Name.Trim();
        tour.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        tour.CoverImageUrl = string.IsNullOrWhiteSpace(request.CoverImageUrl) ? null : request.CoverImageUrl.Trim();
        tour.IsActive = request.IsActive;
        tour.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "Đã cập nhật tour." });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var tour = await _dbContext.Tours.Include(item => item.Stops).FirstOrDefaultAsync(item => item.Id == id);
        if (tour is null)
        {
            return NotFound(new { message = "Không tìm thấy tour." });
        }

        _dbContext.Tours.Remove(tour);
        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "Đã xóa tour." });
    }

    [HttpPost("{id:int}/stops")]
    public async Task<IActionResult> ReplaceStops(int id, [FromBody] ReplaceStopsRequest request)
    {
        var tour = await _dbContext.Tours.Include(item => item.Stops).FirstOrDefaultAsync(item => item.Id == id);
        if (tour is null)
        {
            return NotFound(new { message = "Không tìm thấy tour." });
        }

        var normalizedStops = (request.Stops ?? [])
            .Where(stop => stop.PoiId > 0)
            .OrderBy(stop => stop.SortOrder)
            .ToList();

        if (normalizedStops.Count == 0)
        {
            tour.Stops.Clear();
            tour.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Đã cập nhật danh sách điểm dừng.", stopCount = 0 });
        }

        var poiIds = normalizedStops.Select(stop => stop.PoiId).Distinct().ToList();
        var validPoiIds = await _dbContext.POIs.AsNoTracking().Where(item => poiIds.Contains(item.Id)).Select(item => item.Id).ToListAsync();
        if (validPoiIds.Count != poiIds.Count)
        {
            return BadRequest(new { message = "Danh sách POI trong tour có phần tử không hợp lệ." });
        }

        _dbContext.TourStops.RemoveRange(tour.Stops);
        await _dbContext.SaveChangesAsync();

        var stopsToInsert = normalizedStops
            .Select((stop, index) => new TourStop
            {
                TourId = id,
                PoiId = stop.PoiId,
                SortOrder = index + 1,
                Note = string.IsNullOrWhiteSpace(stop.Note) ? null : stop.Note.Trim()
            })
            .ToList();

        _dbContext.TourStops.AddRange(stopsToInsert);
        tour.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Đã cập nhật danh sách điểm dừng.", stopCount = stopsToInsert.Count });
    }

    public sealed class UpsertTourRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public sealed class ReplaceStopsRequest
    {
        public List<TourStopRequest>? Stops { get; set; }
    }

    public sealed class TourStopRequest
    {
        public int PoiId { get; set; }
        public int SortOrder { get; set; }
        public string? Note { get; set; }
    }
}
