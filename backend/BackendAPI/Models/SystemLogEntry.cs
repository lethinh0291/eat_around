using System.ComponentModel.DataAnnotations;

namespace BackendAPI.Models;

public class SystemLogEntry
{
    [Key]
    public int Id { get; set; }

    [MaxLength(40)]
    public string Category { get; set; } = "system";

    [MaxLength(20)]
    public string Level { get; set; } = "info";

    [MaxLength(200)]
    public string Source { get; set; } = string.Empty;

    [MaxLength(600)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(1200)]
    public string? Details { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
