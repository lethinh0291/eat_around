using BackendAPI.Models;

namespace BackendAPI.Services;

/// <summary>
/// Interface cho QR Code generation service
/// </summary>
public interface IQRGeneratorService
{
    /// <summary>
    /// Tạo QR trigger cho một POI với ngôn ngữ cụ thể
    /// </summary>
    /// <param name="poiId">ID của POI</param>
    /// <param name="languageCode">Mã ngôn ngữ (mặc định "vi")</param>
    /// <returns>QRTrigger object chứa QR content và image</returns>
    Task<QRTrigger> GenerateQRTriggerAsync(int poiId, string languageCode = "vi");

    /// <summary>
    /// Tạo QR content từ POI ID và language code
    /// Format: {poiId}|{languageCode}
    /// </summary>
    /// <param name="poiId">ID của POI</param>
    /// <param name="languageCode">Mã ngôn ngữ</param>
    /// <returns>QR content string</returns>
    string GenerateQRContent(int poiId, string languageCode = "vi");

    /// <summary>
    /// Tạo QR code PNG image từ content
    /// </summary>
    /// <param name="content">Nội dung cần encode thành QR</param>
    /// <param name="pixelPerModule">Kích thước pixel mỗi module (mặc định 10)</param>
    /// <returns>Base64 encoded PNG image</returns>
    Task<string> GenerateQRImageBase64Async(string content, int pixelPerModule = 10);

    /// <summary>
    /// Tạo QR code PNG image SVG format từ content
    /// </summary>
    /// <param name="content">Nội dung cần encode thành QR</param>
    /// <returns>SVG string</returns>
    string GenerateQRImageSvg(string content);
}
