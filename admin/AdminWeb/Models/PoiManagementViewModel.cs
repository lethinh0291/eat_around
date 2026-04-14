using SharedLib.Models;

namespace AdminWeb.Models;

public sealed class PoiManagementViewModel
{
    public List<PoiListItemViewModel> Items { get; set; } = [];
    public string Search { get; set; } = string.Empty;
    public string Status { get; set; } = "all";
    public bool IsAdmin { get; set; }
    public bool IsSeller { get; set; }
    public string CurrentOwnerDisplayName { get; set; } = string.Empty;

    public int TotalCount => Items.Count;
}

public sealed class PoiListItemViewModel
{
    public required POI Poi { get; init; }
    public string OwnerName { get; init; } = "Chua gan";
    public string StatusLabel { get; init; } = "Chua co QR";
    public string StatusCode { get; init; } = "no-qr";
    public DateTime? CreatedAtUtc { get; init; }
    public int? LatestQrId { get; init; }
}
