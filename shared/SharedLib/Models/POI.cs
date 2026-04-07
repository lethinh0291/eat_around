using System.ComponentModel.DataAnnotations;
namespace SharedLib.Models;

public class POI
{
    [Key]
    public int Id { get; set; }

    [Required(ErrorMessage = "Tên không được để trống")]
    [StringLength(100, ErrorMessage = "Tên không được vượt quá 100 ký tự")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Vĩ độ không được để trống")]
    [Range(-90, 90, ErrorMessage = "Vĩ độ phải nằm trong khoảng -90 đến 90")]
    public double Latitude { get; set; }

    [Required(ErrorMessage = "Kinh độ không được để trống")]
    [Range(-180, 180, ErrorMessage = "Kinh độ phải nằm trong khoảng -180 đến 180")]
    public double Longitude { get; set; }


    [Range(1, 1000, ErrorMessage = "Bán kính phải nằm trong khoảng 1 đến 1000 mét")]
    public double Radius { get; set; } = 100; // Mặc định bán kính là 100 mét

    [Range(1, 10, ErrorMessage = "Độ ưu tiên phải nằm trong khoảng 1 (thấp) đến 10 (cao)")]
    public int Priority { get; set; } = 1; // Mặc định độ ưu tiên

    public string? ImageUrl { get; set; } // URL hình ảnh đại diện cho POI
    public string? AudioUrl { get; set; } // URL file âm thanh hướng dẫn

    [StringLength(10)]
    public string LanguageCode { get; set; } = "vi"; // Mặc định tiếng Việt [cite: 45]
}