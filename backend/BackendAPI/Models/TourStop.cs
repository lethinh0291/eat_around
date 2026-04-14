using System.ComponentModel.DataAnnotations;

namespace BackendAPI.Models;

public class TourStop
{
    [Key]
    public int Id { get; set; }

    public int TourId { get; set; }

    public int PoiId { get; set; }

    public int SortOrder { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    public Tour? Tour { get; set; }
}
