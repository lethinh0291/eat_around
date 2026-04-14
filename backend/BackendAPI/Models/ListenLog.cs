using System.ComponentModel.DataAnnotations;

namespace BackendAPI.Models;

public class ListenLog
{
    [Key]
    public int Id { get; set; }

    [MaxLength(120)]
    public string DeviceId { get; set; } = string.Empty;

    public int PoiId { get; set; }

    [MaxLength(10)]
    public string LanguageCode { get; set; } = "vi";

    [MaxLength(20)]
    public string ContentType { get; set; } = "tts";

    public double DurationSeconds { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public DateTime PlayedAtUtc { get; set; } = DateTime.UtcNow;
}
