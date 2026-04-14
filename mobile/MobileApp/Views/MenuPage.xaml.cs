using MobileApp.Services;

namespace ZesTour.Views;

public partial class MenuPage : ContentPage
{
    private readonly AppNavigator _navigator;
    private readonly AuthService _authService;
    private readonly ApiService _apiService;
    private bool _isBannerLoaded;
    private bool _isSidebarOpen;
    private bool _isSidebarAnimating;

    public MenuPage(AppNavigator navigator, AuthService authService)
    {
        _navigator = navigator;
        _authService = authService;
        _apiService = new ApiService();
        InitializeComponent();
        BindingContext = new { User = _authService.CurrentUser };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CloseSidebarAsync(false);
        UpdateRoleBadge();
        var isSeller = IsSeller();
        StoreRegistrationCard.IsVisible = isSeller;
        StoreManagementCard.IsVisible = isSeller;

        if (!_isBannerLoaded)
        {
            await LoadBannersAsync();
            _isBannerLoaded = true;
        }
    }

    private async Task LoadBannersAsync()
    {
        var banners = await _apiService.GetActiveAdBannersAsync();
        if (banners.Count == 0)
        {
            BannerSection.IsVisible = false;
            BannerCarousel.ItemsSource = null;
            return;
        }

        BannerCarousel.ItemsSource = banners;
        BannerSection.IsVisible = true;
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

    private async void OnOpenMapClicked(object? sender, EventArgs e)
    {
        await _navigator.ShowMainFromMenuAsync();
    }

    private async void OnOpenSidebarTapped(object? sender, TappedEventArgs e)
    {
        await OpenSidebarAsync();
    }

    private async void OnSidebarBackdropTapped(object? sender, TappedEventArgs e)
    {
        await CloseSidebarAsync();
    }

    private async void OnSidebarHomeTapped(object? sender, EventArgs e)
    {
        await CloseSidebarAsync();
        await _navigator.ShowMainFromMenuAsync();
    }

    private async void OnSidebarProfileTapped(object? sender, EventArgs e)
    {
        await CloseSidebarAsync();
        await _navigator.ShowProfileAsync();
    }

    private async void OnSidebarSettingsTapped(object? sender, EventArgs e)
    {
        await CloseSidebarAsync();
        await _navigator.ShowSettingsAsync();
    }

    private async void OnSidebarSupportTapped(object? sender, EventArgs e)
    {
        await CloseSidebarAsync();
        await _navigator.ShowHelpFeedbackAsync();
    }

    private async void OnSidebarLogoutTapped(object? sender, EventArgs e)
    {
        await CloseSidebarAsync();
        await _authService.SignOutAsync();
        await _navigator.ShowLoginAsync();
    }

    private async Task OpenSidebarAsync()
    {
        if (_isSidebarOpen || _isSidebarAnimating)
        {
            return;
        }

        _isSidebarAnimating = true;
        SidebarBackdrop.IsVisible = true;
        SidebarBackdrop.InputTransparent = false;
        SidebarFab.IsVisible = false;

        await Task.WhenAll(
            SidebarBackdrop.FadeToAsync(1, 160, Easing.CubicOut),
            SidebarPanel.TranslateToAsync(0, 0, 180, Easing.CubicOut)
        );

        _isSidebarOpen = true;
        _isSidebarAnimating = false;
    }

    private async Task CloseSidebarAsync(bool animated = true)
    {
        if ((!_isSidebarOpen && SidebarBackdrop.IsVisible == false) || _isSidebarAnimating)
        {
            return;
        }

        _isSidebarAnimating = true;

        if (animated)
        {
            await Task.WhenAll(
                SidebarBackdrop.FadeToAsync(0, 140, Easing.CubicIn),
                SidebarPanel.TranslateToAsync(320, 0, 170, Easing.CubicIn)
            );
        }
        else
        {
            SidebarBackdrop.Opacity = 0;
            SidebarPanel.TranslationX = 320;
        }

        SidebarBackdrop.IsVisible = false;
        SidebarBackdrop.InputTransparent = true;
        SidebarFab.IsVisible = true;
        _isSidebarOpen = false;
        _isSidebarAnimating = false;
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
            RoleBadgeBorder.BackgroundColor = Color.FromArgb("#FFF1E8");
            RoleBadgeLabel.TextColor = Color.FromArgb("#8E2F18");
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
        RoleBadgeBorder.BackgroundColor = Color.FromArgb("#FFE8DD");
        RoleBadgeLabel.TextColor = Color.FromArgb("#A53A1D");
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

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        await _authService.SignOutAsync();
        await _navigator.ShowLoginAsync();
    }
}
