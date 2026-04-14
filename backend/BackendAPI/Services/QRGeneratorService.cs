using BackendAPI.Data;
using BackendAPI.Models;
using QRCoder;

namespace BackendAPI.Services;

/// <summary>
/// Implementation của QR Code generation service
/// Sử dụng QRCoder library để tạo QR codes
/// </summary>
public class QRGeneratorService : IQRGeneratorService
{
    private readonly AppDbContext _context;
    private readonly ILogger<QRGeneratorService> _logger;

    public QRGeneratorService(AppDbContext context, ILogger<QRGeneratorService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Tạo QR trigger cho một POI với ngôn ngữ cụ thể
    /// </summary>
    public async Task<QRTrigger> GenerateQRTriggerAsync(int poiId, string languageCode = "vi")
    {
        try
        {
            _logger.LogInformation($"Generating QR trigger for POI ID: {poiId}, Language: {languageCode}");

            // Kiểm tra POI có tồn tại không
            var poi = await _context.POIs.FindAsync(poiId);
            if (poi == null)
            {
                throw new InvalidOperationException($"POI with ID {poiId} not found");
            }

            // Tạo QR content
            var qrContent = GenerateQRContent(poiId, languageCode);

            // Tạo QR image
            var qrImageBase64 = await GenerateQRImageBase64Async(qrContent);

            // Tạo QRTrigger object
            var qrTrigger = new QRTrigger
            {
                PoiId = poiId,
                LanguageCode = languageCode,
                QrContent = qrContent,
                QrImageBase64 = qrImageBase64,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                Status = "Active"
            };

            _logger.LogInformation($"QR trigger generated successfully for POI ID: {poiId}");
            return qrTrigger;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error generating QR trigger: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Tạo QR content từ POI ID và language code
    /// Format: {poiId}|{languageCode}
    /// </summary>
    public string GenerateQRContent(int poiId, string languageCode = "vi")
    {
        // Format: poiId|languageCode (chẳng hạn: 123|vi)
        // Mobile app sẽ parse format này để lấy poiId và languageCode
        return $"{poiId}|{languageCode}";
    }

    /// <summary>
    /// Tạo QR code PNG image từ content sử dụng QRCoder
    /// </summary>
    public async Task<string> GenerateQRImageBase64Async(string content, int pixelPerModule = 10)
    {
        try
        {
            return await Task.Run(() =>
            {
                using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
                {
                    QRCodeData qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
                    using (var qrCode = new PngByteQRCode(qrCodeData))
                    {
                        byte[] qrCodeImage = qrCode.GetGraphic(pixelPerModule);
                        return Convert.ToBase64String(qrCodeImage);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error generating QR image: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Tạo QR code ASCII format từ content (đơn giản)
    /// </summary>
    public string GenerateQRImageSvg(string content)
    {
        try
        {
            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            {
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
                using (var qrCode = new AsciiQRCode(qrCodeData))
                {
                    return qrCode.GetGraphic(1); // Simple ASCII representation
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error generating QR ASCII: {ex.Message}");
            // For SVG, QRCoder might not have direct SVG support
            // Return base64 PNG as fallback
            return GenerateQRImageBase64Async(content).Result;
        }
    }
}
