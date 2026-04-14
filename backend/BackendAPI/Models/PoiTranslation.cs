using System.ComponentModel.DataAnnotations;

namespace BackendAPI.Models;

public class PoiTranslation
{
    [Key]
    public int Id { get; set; }

    public int PoiId { get; set; }

    [Required]
    [MaxLength(10)]
    public string LanguageCode { get; set; } = "vi";

    [MaxLength(200)]
    public string? Title { get; set; }

    [MaxLength(3000)]
    public string ContentText { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? AudioUrl { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "pending";

    [MaxLength(120)]
    public string? SubmittedBy { get; set; }

    [MaxLength(120)]
    public string? ReviewedBy { get; set; }

    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAtUtc { get; set; }
}
