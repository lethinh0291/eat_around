using MobileApp.Services;
using MobileApp.Resources.Localization;
using System.IO;

namespace ZesTour.Views;

public partial class MenuPage : ContentPage
{
    private const double WelcomeImageScale = 1.02;
    private const double WelcomeImagePanFraction = 0.08;

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
        RefreshUserBinding();
        UpdateAvatarDisplay();
        WelcomeCardHost.SizeChanged += OnWelcomeCardHostSizeChanged;
    }

    private void ApplyLocalizedText()
    {
        HelloLabel.Text = AppText.Get("Menu_Hello");
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
        RefreshUserBinding();
        UpdateRoleBadge();
        UpdateAvatarDisplay();
        UpdateWelcomeCardBackground();
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

    private async void OnOpenMapTapped(object? sender, TappedEventArgs e)
    {
        await AnimateCardPressedAsync(sender);
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

    private void UpdateAvatarDisplay()
    {
        var user = _authService.CurrentUser;
        if (user is null)
        {
            AvatarImage.Source = null;
            AvatarImage.IsVisible = false;
            AvatarInitialsLabel.Text = "U";
            AvatarInitialsLabel.IsVisible = true;
            return;
        }

        AvatarInitialsLabel.Text = _authService.GetInitials(user);

        if (!string.IsNullOrWhiteSpace(user.AvatarUrl) && Uri.TryCreate(user.AvatarUrl, UriKind.Absolute, out var avatarUri))
        {
            AvatarImage.Source = ImageSource.FromUri(avatarUri);
            AvatarImage.IsVisible = true;
            AvatarInitialsLabel.IsVisible = false;
            return;
        }

        AvatarImage.Source = null;
        AvatarImage.IsVisible = false;
        AvatarInitialsLabel.IsVisible = true;
    }

    private void RefreshUserBinding()
    {
        BindingContext = new { User = _authService.CurrentUser };
    }

    private void UpdateWelcomeCardBackground()
    {
        var (sourceValue, isBuiltInAsset) = _authService.GetMenuWelcomeImageSource();
        var hasCustomBackground = !string.IsNullOrWhiteSpace(sourceValue) &&
                                  (isBuiltInAsset || File.Exists(sourceValue));

        if (hasCustomBackground)
        {
            WelcomeCardImage.Source = ImageSource.FromFile(sourceValue!);
            WelcomeCardImage.IsVisible = true;
            WelcomeCardOverlay.IsVisible = true;
            WelcomeCardFallbackTint.IsVisible = false;
            WelcomeCardBorder.BackgroundColor = Color.FromArgb("#DAB29A");
            WelcomeCardImage.Scale = WelcomeImageScale;
            ApplyWelcomeCardImageCrop();
            return;
        }

        WelcomeCardImage.Source = null;
        WelcomeCardImage.IsVisible = false;
        WelcomeCardOverlay.IsVisible = false;
        WelcomeCardFallbackTint.IsVisible = true;
        WelcomeCardImage.TranslationX = 0;
        WelcomeCardImage.TranslationY = 0;
        WelcomeCardImage.WidthRequest = -1;
        WelcomeCardImage.HeightRequest = -1;
        WelcomeCardImage.Scale = 1;
        WelcomeCardBorder.BackgroundColor = Application.Current?.Resources.TryGetValue("Primary", out var primaryColor) == true && primaryColor is Color color
            ? color
            : Color.FromArgb("#F55B23");
    }

    private void OnWelcomeCardHostSizeChanged(object? sender, EventArgs e)
    {
        if (!WelcomeCardImage.IsVisible)
        {
            return;
        }

        ApplyWelcomeCardImageCrop();
    }

    private void ApplyWelcomeCardImageCrop()
    {
        var width = WelcomeCardHost.Width;
        var height = WelcomeCardHost.Height;
        if (width <= 1 || height <= 1)
        {
            return;
        }

        WelcomeCardImage.WidthRequest = width;
        WelcomeCardImage.HeightRequest = height;

        var (offsetX, offsetY) = _authService.GetMenuWelcomeCropOffset();
        var maxX = width * WelcomeImagePanFraction;
        var maxY = height * WelcomeImagePanFraction;

        WelcomeCardImage.TranslationX = maxX * offsetX;
        WelcomeCardImage.TranslationY = maxY * offsetY;
    }

    private async void OnMyTripsTapped(object? sender, TappedEventArgs e)
    {
        await AnimateCardPressedAsync(sender);
        await _navigator.ShowMyTripsAsync();
    }

    private async void OnAccountTapped(object? sender, TappedEventArgs e)
    {
        await AnimateCardPressedAsync(sender);
        await _navigator.ShowProfileAsync();
    }

    private async void OnSettingsTapped(object? sender, TappedEventArgs e)
    {
        await AnimateCardPressedAsync(sender);
        await _navigator.ShowSettingsAsync();
    }

    private async void OnSupportTapped(object? sender, TappedEventArgs e)
    {
        await AnimateCardPressedAsync(sender);
        await _navigator.ShowHelpFeedbackAsync();
    }

    private async void OnQuickNarrationTapped(object? sender, TappedEventArgs e)
    {
        await _navigator.ShowQrTriggerAsync();
    }

    private async void OnQrTriggerTapped(object? sender, TappedEventArgs e)
    {
        await AnimateCardPressedAsync(sender);
        await _navigator.ShowQrTriggerAsync();
    }

    private async void OnStoreRegistrationTapped(object? sender, TappedEventArgs e)
    {
        await AnimateCardPressedAsync(sender);

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
        await AnimateCardPressedAsync(sender);

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

    private static async Task AnimateCardPressedAsync(object? sender)
    {
        if (sender is not Border card)
        {
            return;
        }

        var originalScale = card.Scale;
        var originalColor = card.BackgroundColor;
        var pressedColor = BlendColor(card.BackgroundColor, Color.FromArgb("#0F172A"), 0.09f);

        card.BackgroundColor = pressedColor;
        await card.ScaleToAsync(0.985, 70, Easing.CubicOut);
        await Task.Delay(30);
        card.BackgroundColor = originalColor;
        await card.ScaleToAsync(originalScale, 110, Easing.CubicIn);
    }

    private static Color BlendColor(Color baseColor, Color targetColor, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);

        var r = (baseColor.Red * (1f - amount)) + (targetColor.Red * amount);
        var g = (baseColor.Green * (1f - amount)) + (targetColor.Green * amount);
        var b = (baseColor.Blue * (1f - amount)) + (targetColor.Blue * amount);
        var a = (baseColor.Alpha * (1f - amount)) + (targetColor.Alpha * amount);

        return new Color(r, g, b, a);
    }
}
