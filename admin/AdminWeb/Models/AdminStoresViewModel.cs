using AdminWeb.Services;
using SharedLib.Models;

namespace AdminWeb.Models;

public class AdminStoresViewModel
{
    public List<AdminManagementApiClient.AdminStoreRegistrationDto> PendingRegistrations { get; set; } = [];
    public List<POI> Restaurants { get; set; } = [];
}
