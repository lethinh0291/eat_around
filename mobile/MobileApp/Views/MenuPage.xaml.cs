using MobileApp.Services;

namespace ZesTour.Views;

public partial class MenuPage : ContentPage
{
    private readonly AppNavigator _navigator;
    private readonly AuthService _authService;

    public MenuPage(AppNavigator navigator, AuthService authService)
    {
        _navigator = navigator;
        _authService = authService;
        InitializeComponent();
        BindingContext = new { User = _authService.CurrentUser };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateRoleBadge();
        var isSeller = IsSeller();
        StoreRegistrationCard.IsVisible = isSeller;
        StoreManagementCard.IsVisible = isSeller;
    }

    private async Task ShowComingSoonAsync(string featureName)
    {
        await DisplayAlertAsync("Tính năng", $"{featureName} sẽ được hoàn thiện trong bản cập nhật tiếp theo.", "Đóng");
    }

    private async void OnSearchCompleted(object? sender, EventArgs e)
    {
        var keyword = SearchEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return;
        }

        if (keyword.Contains("bản đồ", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("chỉ đường", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("discover", StringComparison.OrdinalIgnoreCase))
        {
            await _navigator.ShowMainFromMenuAsync();
            return;
        }

        if (keyword.Contains("hồ sơ", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("profile", StringComparison.OrdinalIgnoreCase))
        {
            await _navigator.ShowProfileAsync();
            return;
        }

        if (keyword.Contains("cửa hàng", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("đăng ký", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsSeller())
            {
                await DisplayAlertAsync("Vai trò chưa phù hợp", "Tính năng đăng ký cửa hàng chỉ hiển thị cho tài khoản Người bán.", "OK");
                return;
            }

            if (keyword.Contains("quản lý", StringComparison.OrdinalIgnoreCase))
            {
                await _navigator.ShowStoreManagementAsync();
            }
            else
            {
                await _navigator.ShowStoreRegistrationAsync();
            }
            return;
        }

        if (keyword.Contains("hành trình", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("trip", StringComparison.OrdinalIgnoreCase))
        {
            await _navigator.ShowMyTripsAsync();
            return;
        }

        if (keyword.Contains("cài đặt", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("setting", StringComparison.OrdinalIgnoreCase))
        {
            await _navigator.ShowSettingsAsync();
            return;
        }

        if (keyword.Contains("phản hồi", StringComparison.OrdinalIgnoreCase) ||
            keyword.Contains("trợ giúp", StringComparison.OrdinalIgnoreCase))
        {
            await _navigator.ShowHelpFeedbackAsync();
            return;
        }

        await ShowComingSoonAsync($"Không tìm thấy mục khớp với: {keyword}");
    }

    private async void OnOpenMapTapped(object? sender, TappedEventArgs e)
    {
        await _navigator.ShowMainFromMenuAsync();
    }

    private async void OnMyTripsTapped(object? sender, TappedEventArgs e)
    {
        await _navigator.ShowMyTripsAsync();
    }

    private async void OnAccountTapped(object? sender, TappedEventArgs e)
    {
        await ShowComingSoonAsync("Tài khoản và thanh toán");
    }

    private async void OnSettingsTapped(object? sender, TappedEventArgs e)
    {
        await _navigator.ShowSettingsAsync();
    }

    private async void OnSupportTapped(object? sender, TappedEventArgs e)
    {
        await _navigator.ShowHelpFeedbackAsync();
    }

    private async void OnQuickNarrationTapped(object? sender, TappedEventArgs e)
    {
        await _navigator.ShowHelpFeedbackAsync();
    }

    private async void OnStoreRegistrationTapped(object? sender, TappedEventArgs e)
    {
        if (!IsSeller())
        {
            await DisplayAlertAsync("Vai trò chưa phù hợp", "Bạn cần đăng nhập bằng tài khoản Người bán để dùng tính năng này.", "OK");
            return;
        }

        await _navigator.ShowStoreRegistrationAsync();
    }

    private async void OnStoreManagementTapped(object? sender, TappedEventArgs e)
    {
        if (!IsSeller())
        {
            await DisplayAlertAsync("Vai trò chưa phù hợp", "Bạn cần đăng nhập bằng tài khoản Người bán để dùng tính năng này.", "OK");
            return;
        }

        await _navigator.ShowStoreManagementAsync();
    }

    private bool IsSeller()
    {
        return string.Equals(_authService.CurrentUser?.Role, "seller", StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateRoleBadge()
    {
        var role = _authService.CurrentUser?.Role?.Trim().ToLowerInvariant() ?? "customer";

        if (role == "seller")
        {
            RoleBadgeLabel.Text = "Người bán";
            RoleBadgeBorder.BackgroundColor = Color.FromArgb("#DCFCE7");
            RoleBadgeLabel.TextColor = Color.FromArgb("#166534");
            return;
        }

        if (role == "admin")
        {
            RoleBadgeLabel.Text = "Quản trị viên";
            RoleBadgeBorder.BackgroundColor = Color.FromArgb("#FEE2E2");
            RoleBadgeLabel.TextColor = Color.FromArgb("#991B1B");
            return;
        }

        RoleBadgeLabel.Text = "Khách hàng";
        RoleBadgeBorder.BackgroundColor = Color.FromArgb("#E0F2FE");
        RoleBadgeLabel.TextColor = Color.FromArgb("#075985");
    }

    private async void OnProfileTapped(object? sender, TappedEventArgs e)
    {
        await _navigator.ShowProfileAsync();
    }

    private async void OnSignOutClicked(object? sender, EventArgs e)
    {
        await _authService.SignOutAsync();
        await _navigator.ShowLoginAsync();
    }
}
