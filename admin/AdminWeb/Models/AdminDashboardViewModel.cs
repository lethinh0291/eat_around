using SharedLib.Models;

namespace AdminWeb.Models;

public class AdminDashboardViewModel
{
    public int TotalPois { get; set; }

    public int HighPriorityPois { get; set; }

    public double AverageRadius { get; set; }

    public double AveragePriority { get; set; }

    public int DistinctLanguages { get; set; }

    public int MediaPois { get; set; }

    public List<POI> RecentPois { get; set; } = [];

    public List<AdminModuleCardViewModel> ModuleCards { get; set; } = [];
}

public class AdminModuleCardViewModel
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? Badge { get; set; }

    public string? ActionUrl { get; set; }
}
