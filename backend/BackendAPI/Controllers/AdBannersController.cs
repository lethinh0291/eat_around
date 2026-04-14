using BackendAPI.Data;
using BackendAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendAPI.Controllers;

[ApiController]
[Route("api/ad-banners")]
public class AdBannersController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public AdBannersController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _dbContext.AdBanners
            .OrderBy(item => item.SortOrder)
            .ThenByDescending(item => item.CreatedAtUtc)
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var items = await _dbContext.AdBanners
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenByDescending(item => item.CreatedAtUtc)
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _dbContext.AdBanners.FindAsync(id);
        if (item is null)
        {
            return NotFound(new { message = "Không tìm thấy banner." });
        }

        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertAdBannerRequest request)
    {
        var imageUrl = request.ImageUrl?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return BadRequest(new { message = "Vui lòng nhập URL ảnh banner." });
        }

        var item = new AdBanner
        {
            ImageUrl = imageUrl,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _dbContext.AdBanners.Add(item);
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Đã thêm banner quảng cáo." });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertAdBannerRequest request)
    {
        var imageUrl = request.ImageUrl?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return BadRequest(new { message = "Vui lòng nhập URL ảnh banner." });
        }

        var item = await _dbContext.AdBanners.FindAsync(id);
        if (item is null)
        {
            return NotFound(new { message = "Không tìm thấy banner." });
        }

        item.ImageUrl = imageUrl;
        item.IsActive = request.IsActive;
        item.SortOrder = request.SortOrder;
        item.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "Đã cập nhật banner." });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _dbContext.AdBanners.FindAsync(id);
        if (item is null)
        {
            return NotFound(new { message = "Không tìm thấy banner." });
        }

        _dbContext.AdBanners.Remove(item);
        await _dbContext.SaveChangesAsync();

        return Ok(new { message = "Đã xóa banner." });
    }

    public sealed class UpsertAdBannerRequest
    {
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
    }
}
