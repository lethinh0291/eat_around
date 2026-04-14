using SharedLib.Models;

namespace AdminWeb.Models;

public class AdminDashboardViewModel
{
    public int TotalPois { get; set; }

    public int TotalUsers { get; set; }

    public int SellerUsers { get; set; }

    public int AdminUsers { get; set; }

    public int PendingStoreRegistrations { get; set; }

    public int DistinctLanguages { get; set; }

    public int MediaPois { get; set; }

    public int HighPriorityPois { get; set; }

    public double AverageRadius { get; set; }

    public double AveragePriority { get; set; }

    public List<POI> FeaturedPois { get; set; } = [];

    public List<DashboardMetricCardViewModel> MetricCards { get; set; } = [];

    public List<DashboardSectionViewModel> Sections { get; set; } = [];

    public List<DashboardActivityViewModel> Activities { get; set; } = [];

    public List<DashboardSettingViewModel> Settings { get; set; } = [];
}

public class DashboardMetricCardViewModel
{
    public string Title { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public string Tone { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    public string? ActionUrl { get; set; }

    public string? ActionText { get; set; }
}

public class DashboardSectionViewModel
{
    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Badge { get; set; } = string.Empty;

    public string Tone { get; set; } = string.Empty;

    public string? ActionUrl { get; set; }

    public string? ActionText { get; set; }

    public List<string> Items { get; set; } = [];
}

public class DashboardActivityViewModel
{
    public string TimeLabel { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Tone { get; set; } = string.Empty;
}

public class DashboardSettingViewModel
{
    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;
}
