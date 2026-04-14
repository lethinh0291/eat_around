using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SharedLib.Models;

namespace BackendAPI.Models;

/// <summary>
/// QR Trigger model - Lưu trữ QR code được tạo tự động cho mỗi POI
/// QR code được tạo từ POI ID và ngôn ngữ, dùng để trigger nội dung khi quét
/// </summary>
public class QRTrigger
{
    [Key]
    public int Id { get; set; }

    [Required]
    [ForeignKey("POI")]
    public int PoiId { get; set; }

    [Required(ErrorMessage = "QR Code content không được để trống")]
    [StringLength(2048, ErrorMessage = "QR Code content không được vượt quá 2048 ký tự")]
    public string QrContent { get; set; } = string.Empty;

    [Required]
    [StringLength(10, ErrorMessage = "Language code không được vượt quá 10 ký tự")]
    public string LanguageCode { get; set; } = "vi";

    /// <summary>
    /// Base64 encoded PNG image của QR code
    /// </summary>
    [StringLength(int.MaxValue)]
    public string? QrImageBase64 { get; set; }

    /// <summary>
    /// URL tới image QR code được lưu trữ
    /// </summary>
    [StringLength(1000)]
    public string? QrImageUrl { get; set; }

    /// <summary>
    /// Timestamp khi QR trigger được tạo
    /// </summary>
    [Required]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp lần cuối cập nhật QR trigger
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Số lần QR code này đã được quét (tracking)
    /// </summary>
    public int ScanCount { get; set; } = 0;

    /// <summary>
    /// Trạng thái QR trigger (Active/Inactive)
    /// </summary>
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Active";

    // Navigation property
    public virtual POI? POI { get; set; }
}
