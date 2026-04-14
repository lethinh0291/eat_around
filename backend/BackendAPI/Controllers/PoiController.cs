using SharedLib.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BackendAPI.Data;
using BackendAPI.Services;
using BackendAPI.Models;

namespace BackendAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PoiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IQRGeneratorService _qrGeneratorService;
    private readonly ILogger<PoiController> _logger;

    public PoiController(AppDbContext context, IQRGeneratorService qrGeneratorService, ILogger<PoiController> logger)
    {
        _context = context;
        _qrGeneratorService = qrGeneratorService;
        _logger = logger;
    }
    //1. Get all POIs
    [HttpGet]
    public async Task<ActionResult<IEnumerable<POI>>> GetAll()
    {
        return await _context.POIs.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<POI>> GetById(int id)
    {
        var poi = await _context.POIs.FindAsync(id);
        if (poi == null) return NotFound(new { Message = $"POI với mã ID {id} không tồn tại." });
        return poi;
    }

    //2. Post Create POI and automatically generate QR trigger
    [HttpPost]
    public async Task<ActionResult<POI>> Create(POI poi)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        _context.POIs.Add(poi);
        await _context.SaveChangesAsync();

        // Automatically generate QR trigger for the newly created POI
        try
        {
            var languageCode = string.IsNullOrEmpty(poi.LanguageCode) ? "vi" : poi.LanguageCode;
            var qrTrigger = await _qrGeneratorService.GenerateQRTriggerAsync(poi.Id, languageCode);
            
            _context.QRTriggers.Add(qrTrigger);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"QR trigger generated automatically for POI ID: {poi.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error generating QR trigger for POI {poi.Id}: {ex.Message}");
            // Không throw exception, POI đã được tạo thành công
            // QR generation là optional
        }

        return CreatedAtAction(nameof(GetById), new { id = poi.Id }, poi);
    }

    //3. Update POI
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, POI poi)
    {
        if (id != poi.Id) return BadRequest(new { Message = "ID trong URL không khớp" });
        _context.Entry(poi).State = EntityState.Modified;
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.POIs.Any(e => e.Id == id))
                return NotFound(new { Message = $"POI với mã ID {id} không tồn tại." });
            else throw;
        }
        return NoContent();
    }

    //4. Delete POI
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var poi = await _context.POIs.FindAsync(id);
        if (poi == null) return NotFound(new { Message = $"POI với mã ID {id} không tồn tại." });

        _context.POIs.Remove(poi);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    //5. Generate QR trigger for existing POI
    /// <summary>
    /// Tạo QR trigger cho một POI đã tồn tại
    /// </summary>
    /// <param name="poiId">ID của POI</param>
    /// <param name="languageCode">Mã ngôn ngữ (mặc định "vi")</param>
    /// <returns>QRTrigger object</returns>
    [HttpPost("{poiId}/generate-qr")]
    public async Task<ActionResult<object>> GenerateQRTrigger(int poiId, [FromQuery] string languageCode = "vi")
    {
        try
        {
            // Verify POI exists
            var poi = await _context.POIs.FindAsync(poiId);
            if (poi == null)
                return NotFound(new { Message = $"POI với mã ID {poiId} không tồn tại." });

            // Check if QR trigger already exists for this language
            var existingQR = await _context.QRTriggers
                .FirstOrDefaultAsync(q => q.PoiId == poiId && q.LanguageCode == languageCode);

            if (existingQR != null)
            {
                return BadRequest(new { Message = $"QR trigger đã tồn tại cho POI ID {poiId} với ngôn ngữ {languageCode}." });
            }

            // Generate new QR trigger
            var qrTrigger = await _qrGeneratorService.GenerateQRTriggerAsync(poiId, languageCode);
            _context.QRTriggers.Add(qrTrigger);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                Message = "QR trigger tạo thành công",
                QrContent = qrTrigger.QrContent,
                LanguageCode = qrTrigger.LanguageCode,
                QrImageBase64 = qrTrigger.QrImageBase64?.Substring(0, Math.Min(100, qrTrigger.QrImageBase64.Length)) + "...",
                CreatedAt = qrTrigger.CreatedAtUtc
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error generating QR trigger: {ex.Message}");
            return StatusCode(500, new { Message = "Lỗi khi tạo QR trigger", Error = ex.Message });
        }
    }

    //6. Get all QR triggers for a POI
    /// <summary>
    /// Lấy danh sách tất cả QR triggers cho một POI
    /// </summary>
    /// <param name="poiId">ID của POI</param>
    /// <returns>Danh sách QRTrigger</returns>
    [HttpGet("{poiId}/qr-triggers")]
    public async Task<ActionResult<IEnumerable<object>>> GetQRTriggersForPOI(int poiId)
    {
        var poi = await _context.POIs.FindAsync(poiId);
        if (poi == null)
            return NotFound(new { Message = $"POI với mã ID {poiId} không tồn tại." });

        var qrTriggers = await _context.QRTriggers
            .Where(q => q.PoiId == poiId && q.Status == "Active")
            .Select(q => new
            {
                q.Id,
                q.QrContent,
                q.LanguageCode,
                q.ScanCount,
                q.CreatedAtUtc,
                ImagePreview = q.QrImageBase64 != null ? q.QrImageBase64.Substring(0, Math.Min(50, q.QrImageBase64.Length)) + "..." : null
            })
            .ToListAsync();

        return Ok(qrTriggers);
    }

    //7. Get QR trigger by ID
    /// <summary>
    /// Lấy QR trigger cụ thể theo ID
    /// </summary>
    [HttpGet("qr-triggers/{qrId}")]
    public async Task<ActionResult<object>> GetQRTriggerById(int qrId)
    {
        var qrTrigger = await _context.QRTriggers.FindAsync(qrId);
        if (qrTrigger == null)
            return NotFound(new { Message = $"QR trigger với mã ID {qrId} không tồn tại." });

        return Ok(new
        {
            qrTrigger.Id,
            qrTrigger.PoiId,
            qrTrigger.QrContent,
            qrTrigger.LanguageCode,
            qrTrigger.QrImageBase64,
            qrTrigger.ScanCount,
            qrTrigger.Status,
            qrTrigger.CreatedAtUtc,
            qrTrigger.UpdatedAtUtc
        });
    }

    //8. Delete QR trigger
    /// <summary>
    /// Xóa QR trigger
    /// </summary>
    [HttpDelete("qr-triggers/{qrId}")]
    public async Task<IActionResult> DeleteQRTrigger(int qrId)
    {
        var qrTrigger = await _context.QRTriggers.FindAsync(qrId);
        if (qrTrigger == null)
            return NotFound(new { Message = $"QR trigger với mã ID {qrId} không tồn tại." });

        _context.QRTriggers.Remove(qrTrigger);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    //9. Track QR scan
    /// <summary>
    /// Ghi nhận lần quét QR code
    /// </summary>
    [HttpPost("qr-triggers/{qrId}/track-scan")]
    public async Task<IActionResult> TrackQRScan(int qrId, [FromBody] Dictionary<string, string>? metadata = null)
    {
        var qrTrigger = await _context.QRTriggers.FindAsync(qrId);
        if (qrTrigger == null)
            return NotFound(new { Message = $"QR trigger với mã ID {qrId} không tồn tại." });

        qrTrigger.ScanCount++;
        qrTrigger.UpdatedAtUtc = DateTime.UtcNow;
        _context.QRTriggers.Update(qrTrigger);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = "Quét QR được ghi nhận thành công",
            QrId = qrId,
            ScanCount = qrTrigger.ScanCount
        });
    }
}