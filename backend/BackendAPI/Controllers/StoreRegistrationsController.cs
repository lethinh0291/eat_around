using BackendAPI.Data;
using BackendAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendAPI.Controllers;

[ApiController]
[Route("api/store-registrations")]
public class StoreRegistrationsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public StoreRegistrationsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] StoreRegistration request)
    {
        var storeName = request.StoreName.Trim();
        var ownerName = request.OwnerName.Trim();
        var phone = request.Phone.Trim();
        var address = request.Address.Trim();
        var primaryImageUrl = request.ImageUrl?.Trim();
        var imageUrls = NormalizeImageUrls(request.ImageUrls, primaryImageUrl);
        var resolvedPrimaryImageUrl = !string.IsNullOrWhiteSpace(primaryImageUrl)
            ? primaryImageUrl
            : imageUrls.FirstOrDefault();
        var resolvedRadiusMeters = ResolveRadiusMeters(request.RadiusMeters);

        if (string.IsNullOrWhiteSpace(storeName) ||
            string.IsNullOrWhiteSpace(ownerName) ||
            string.IsNullOrWhiteSpace(phone) ||
            string.IsNullOrWhiteSpace(address))
        {
            return BadRequest(new { message = "Vui lòng nhập đủ thông tin bắt buộc." });
        }

        _dbContext.StoreRegistrations.Add(new StoreRegistration
        {
            StoreName = storeName,
            OwnerName = ownerName,
            ImageUrl = resolvedPrimaryImageUrl,
            ImageUrls = imageUrls,
            Phone = phone,
            Address = address,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            RadiusMeters = resolvedRadiusMeters,
            Category = request.Category?.Trim() ?? string.Empty,
            Description = request.Description?.Trim() ?? string.Empty,
            SubmittedAtUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "Yêu cầu đăng ký cửa hàng đã được ghi nhận." });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _dbContext.StoreRegistrations
            .OrderByDescending(item => item.SubmittedAtUtc)
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("owner")]
    public async Task<IActionResult> GetByOwner([FromQuery] string ownerName)
    {
        var normalizedOwner = ownerName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedOwner))
        {
            return BadRequest(new { message = "Thiếu tên chủ cửa hàng để lọc dữ liệu." });
        }

        var items = await _dbContext.StoreRegistrations
            .Where(item => item.OwnerName.ToLower() == normalizedOwner.ToLower())
            .OrderByDescending(item => item.SubmittedAtUtc)
            .ToListAsync();

        return Ok(items);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateStoreRegistrationRequest request)
    {
        var ownerName = request.OwnerName.Trim();
        var storeName = request.StoreName.Trim();
        var phone = request.Phone.Trim();
        var address = request.Address.Trim();
        var primaryImageUrl = request.ImageUrl?.Trim();
        var imageUrls = NormalizeImageUrls(request.ImageUrls, primaryImageUrl);
        var resolvedPrimaryImageUrl = !string.IsNullOrWhiteSpace(primaryImageUrl)
            ? primaryImageUrl
            : imageUrls.FirstOrDefault();
        var resolvedRadiusMeters = ResolveRadiusMeters(request.RadiusMeters);

        if (string.IsNullOrWhiteSpace(ownerName) ||
            string.IsNullOrWhiteSpace(storeName) ||
            string.IsNullOrWhiteSpace(phone) ||
            string.IsNullOrWhiteSpace(address))
        {
            return BadRequest(new { message = "Vui lòng nhập đủ thông tin bắt buộc." });
        }

        var item = await _dbContext.StoreRegistrations.FirstOrDefaultAsync(entry => entry.Id == id);
        if (item is null)
        {
            return NotFound(new { message = "Không tìm thấy đăng ký cửa hàng." });
        }

        if (!string.Equals(item.OwnerName.Trim(), ownerName, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        item.StoreName = storeName;
        item.ImageUrl = resolvedPrimaryImageUrl;
        item.ImageUrls = imageUrls;
        item.Phone = phone;
        item.Address = address;
        item.Latitude = request.Latitude;
        item.Longitude = request.Longitude;
        item.RadiusMeters = resolvedRadiusMeters;
        item.Category = request.Category?.Trim() ?? string.Empty;
        item.Description = request.Description?.Trim() ?? string.Empty;

        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "Cập nhật đăng ký cửa hàng thành công." });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] string? ownerName)
    {
        var item = await _dbContext.StoreRegistrations.FirstOrDefaultAsync(entry => entry.Id == id);
        if (item is null)
        {
            return NotFound(new { message = "Không tìm thấy đăng ký cửa hàng." });
        }

        if (!string.IsNullOrWhiteSpace(ownerName) &&
            !string.Equals(item.OwnerName.Trim(), ownerName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        _dbContext.StoreRegistrations.Remove(item);
        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "Đã xóa đăng ký cửa hàng." });
    }

    public sealed class UpdateStoreRegistrationRequest
    {
        public string StoreName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public List<string>? ImageUrls { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? RadiusMeters { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    private static double ResolveRadiusMeters(double? radiusMeters)
    {
        if (radiusMeters is null || radiusMeters <= 0)
        {
            return 140;
        }

        return radiusMeters.Value;
    }

    private static List<string> NormalizeImageUrls(IEnumerable<string>? imageUrls, string? primaryImageUrl = null)
    {
        var normalized = imageUrls?
            .Select(url => url?.Trim())
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        var primary = primaryImageUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(primary) &&
            !normalized.Any(url => string.Equals(url, primary, StringComparison.OrdinalIgnoreCase)))
        {
            normalized.Insert(0, primary);
        }

        return normalized;
    }
}