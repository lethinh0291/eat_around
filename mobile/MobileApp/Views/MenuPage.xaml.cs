using MobileApp.Services;
using MobileApp.Resources.Localization;

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
        ApplyLocalizedText();
        BindingContext = new { User = _authService.CurrentUser };
    }

    private void ApplyLocalizedText()
    {
        HelloLabel.Text = AppText.Get("Menu_Hello");
        SearchEntry.Placeholder = AppText.Get("Menu_SearchPlaceholder");
        ExploreSectionLabel.Text = AppText.Get("Menu_ExploreSection");
        DiscoverTitleLabel.Text = AppText.Get("Menu_DiscoverTitle");
        DiscoverSubtitleLabel.Text = AppText.Get("Menu_DiscoverSubtitle");
        QrTriggerTitleLabel.Text = AppText.Get("Menu_QrTriggerTitle");
        QrTriggerSubtitleLabel.Text = AppText.Get("Menu_QrTriggerSubtitle");
        MyTripsTitleLabel.Text = AppText.Get("Menu_MyTripsTitle");
        MyTripsSubtitleLabel.Text = AppText.Get("Menu_MyTripsSubtitle");
        AccountSectionLabel.Text = AppText.Get("Menu_AccountSection");
        AccountPaymentLabel.Text = AppText.Get("Menu_AccountPayment");
        SettingsMenuLabel.Text = AppText.Get("Settings_Title");
        SellerSectionLabel.Text = AppText.Get("Menu_SellerSection");
        StoreManagementLabel.Text = AppText.Get("Menu_StoreManagement");
        StoreRegistrationLabel.Text = AppText.Get("Menu_StoreRegistration");
        SupportTitleLabel.Text = AppText.Get("Menu_SupportTitle");
        SupportSubtitleLabel.Text = AppText.Get("Menu_SupportSubtitle");
        QuickActionsTitleLabel.Text = AppText.Get("Menu_QuickActionsTitle");
        QuickActionsSubtitleLabel.Text = AppText.Get("Menu_QuickActionsSubtitle");
        SidebarHomeButton.Text = AppText.Get("Menu_Home");
        SidebarProfileButton.Text = AppText.Get("Menu_Profile");
        SidebarSettingsButton.Text = AppText.Get("Settings_Title");
        SidebarSupportButton.Text = AppText.Get("Menu_HelpFeedback");
        SidebarLogoutButton.Text = AppText.Get("Menu_Logout");
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
        await DisplayAlertAsync(
            AppText.Get("Menu_FeatureTitle"),
            AppText.Format("Menu_ComingSoonFormat", featureName),
            AppText.Get("Menu_Close"));
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
                await DisplayAlertAsync(
                    AppText.Get("Menu_RoleMismatchTitle"),
                    AppText.Get("Menu_RoleMismatchSellerFeature"),
                    AppText.Get("Common_Ok"));
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

        await ShowComingSoonAsync(AppText.Format("Menu_NotFoundFormat", keyword));
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
        await ShowComingSoonAsync(AppText.Get("Menu_AccountPayment"));
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
        await _navigator.ShowQrTriggerAsync();
    }

    private async void OnQrTriggerTapped(object? sender, TappedEventArgs e)
    {
        await _navigator.ShowQrTriggerAsync();
    }

    private async void OnStoreRegistrationTapped(object? sender, TappedEventArgs e)
    {
        if (!IsSeller())
        {
            await DisplayAlertAsync(
                AppText.Get("Menu_RoleMismatchTitle"),
                AppText.Get("Menu_RoleMismatchSellerRequired"),
                AppText.Get("Common_Ok"));
            return;
        }

        await _navigator.ShowStoreRegistrationAsync();
    }

    private async void OnStoreManagementTapped(object? sender, TappedEventArgs e)
    {
        if (!IsSeller())
        {
            await DisplayAlertAsync(
                AppText.Get("Menu_RoleMismatchTitle"),
                AppText.Get("Menu_RoleMismatchSellerRequired"),
                AppText.Get("Common_Ok"));
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
            RoleBadgeLabel.Text = AppText.Get("Menu_RoleSeller");
            RoleBadgeBorder.BackgroundColor = Color.FromArgb("#FFF1E8");
            RoleBadgeLabel.TextColor = Color.FromArgb("#8E2F18");
            return;
        }

        if (role == "admin")
        {
            RoleBadgeLabel.Text = AppText.Get("Menu_RoleAdmin");
            RoleBadgeBorder.BackgroundColor = Color.FromArgb("#FEE2E2");
            RoleBadgeLabel.TextColor = Color.FromArgb("#991B1B");
            return;
        }

        RoleBadgeLabel.Text = AppText.Get("Menu_RoleCustomer");
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
