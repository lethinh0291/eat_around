using AdminWeb.Services;

namespace AdminWeb.Models;

public class AdminUsersViewModel
{
    public List<UserRow> Users { get; set; } = [];

    public sealed class UserRow
    {
        public AdminManagementApiClient.AdminUserDto User { get; set; } = new();
        public List<string> OwnedStores { get; set; } = [];
    }
}
