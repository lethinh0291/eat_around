using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BackendAPI.Models;

public class StoreRegistration
{
    public int Id { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    [JsonIgnore]
    public string? ImageUrlsJson { get; set; }

    [NotMapped]
    public List<string> ImageUrls
    {
        get => DeserializeImageUrls(ImageUrlsJson);
        set => ImageUrlsJson = JsonSerializer.Serialize(NormalizeImageUrls(value));
    }

    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? RadiusMeters { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; set; }

    private static List<string> DeserializeImageUrls(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static List<string> NormalizeImageUrls(IEnumerable<string>? imageUrls)
    {
        return imageUrls?
            .Select(url => url?.Trim())
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }
}