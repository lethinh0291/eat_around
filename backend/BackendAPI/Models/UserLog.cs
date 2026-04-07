using System.ComponentModel.DataAnnotations;

namespace BackendAPI.Models;

public class UserLog
{
    [Key]
    public int Id { get; set; }
    public string DeviceId { get; set; } = string.Empty; // ID thiết bị của người dùng
    public int POIId { get; set; } // ID của POI mà người dùng tiếp cận
    public DateTime Timestamp { get; set; } = DateTime.UtcNow; //Thời điem người dùng tiếp cận POI
    public double Duration { get; set; } // Thời gian người dùng ở lại trong phạm vi POI (tính bằng giây)
    public double Lat { get; set; } // Vĩ độ của người dùng khi tiếp cận POI
    public double Lng { get; set; } // Kinh độ của người dùng khi tiếp cận POI
}