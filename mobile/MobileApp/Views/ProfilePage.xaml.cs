using MobileApp.Services;
using MobileApp.Resources.Localization;
using System.IO;
using Microsoft.Maui.Storage;

namespace ZesTour.Views;

public partial class ProfilePage : ContentPage
{
    private const double WelcomePreviewScale = 1.22;
    private const double WelcomePreviewPanFraction = 0.18;

    private readonly AuthService _authService;
    private readonly AppNavigator _navigator;
    private double _welcomePanStartX;
    private double _welcomePanStartY;

    public ProfilePage(AuthService authService, AppNavigator navigator)
    {
        _authService = authService;
        _navigator = navigator;
        InitializeComponent();
        ApplyLocalizedText();
        WelcomePreviewHost.SizeChanged += OnWelcomePreviewHostSizeChanged;
    }

    private void ApplyLocalizedText()
    {
        ProfileTitleLabel.Text = AppText.Get("Profile_Title");
        ProfileSubtitleLabel.Text = AppText.Get("Profile_Subtitle");
        UsernameTitleLabel.Text = AppText.Get("Profile_Username");
        AccountNumberTitleLabel.Text = AppText.Get("Profile_AccountNumber");
        AvatarTitleLabel.Text = AppText.Get("Profile_Avatar");
        AvatarHintLabel.Text = AppText.Get("Profile_AvatarHint");
        ChangeAvatarButton.Text = AppText.Get("Profile_ChangeAvatar");
        ResetMenuWelcomeImageButton.Text = AppText.Get("Profile_ResetMenuWelcomeImage");
        WelcomeEditorTitleLabel.Text = AppText.Get("Profile_WelcomeEditorTitle");
        WelcomeEditorHintLabel.Text = AppText.Get("Profile_WelcomeEditorHint");
        WelcomeEditorTipLabel.Text = AppText.Get("Profile_WelcomeEditorPanTip");
        WelcomePresetLabel.Text = AppText.Get("Profile_WelcomePresets");
        QuickActionsTitleLabel.Text = AppText.Get("Profile_QuickActions");
        MapTitleLabel.Text = AppText.Get("Profile_Map");
        BackToHomeLabel.Text = AppText.Get("Profile_BackToHome");
        StatusTitleLabel.Text = AppText.Get("Profile_Status");
        LocalSyncLabel.Text = AppText.Get("Profile_LocalSync");
        SignOutButton.Text = AppText.Get("Profile_Logout");
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadProfile();
    }

    private void LoadProfile()
    {
        var user = _authService.CurrentUser;
        if (user is null)
        {
            NameLabel.Text = AppText.Get("Profile_NotSignedIn");
            EmailLabel.Text = AppText.Get("Profile_PleaseSignIn");
            UsernameLabel.Text = AppText.Get("Profile_GuestUsername");
            UserIdLabel.Text = "#0000";
            StatusLabel.Text = AppText.Get("Profile_NoSession");
            AvatarInitialsLabel.Text = "U";
            AvatarInitialsLabel.IsVisible = true;
            AvatarImage.IsVisible = false;
            AvatarImage.Source = null;
            RefreshWelcomeBackgroundPreview();
            return;
        }

        NameLabel.Text = user.Name;
        EmailLabel.Text = user.Email;
        UsernameLabel.Text = user.Username;
        UserIdLabel.Text = $"#{user.Id:0000}";
        StatusLabel.Text = AppText.Get("Profile_Active");
        AvatarInitialsLabel.Text = _authService.GetInitials(user);

        if (!string.IsNullOrWhiteSpace(user.AvatarUrl) && Uri.TryCreate(user.AvatarUrl, UriKind.Absolute, out var avatarUri))
        {
            AvatarImage.Source = ImageSource.FromUri(avatarUri);
            AvatarImage.IsVisible = true;
            AvatarInitialsLabel.IsVisible = false;
        }
        else
        {
            AvatarImage.Source = null;
            AvatarImage.IsVisible = false;
            AvatarInitialsLabel.IsVisible = true;
        }

        RefreshWelcomeBackgroundPreview();
    }

    private async void OnChangeAvatarClicked(object? sender, EventArgs e)
    {
        MessageLabel.Text = string.Empty;

        var pickedFile = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = AppText.Get("Profile_PickAvatar"),
            FileTypes = FilePickerFileType.Images
        });

        if (pickedFile is null)
        {
            return;
        }

        try
        {
            await using var stream = await pickedFile.OpenReadAsync();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);

            var result = await _authService.UpdateCurrentUserAvatarAsync(memoryStream.ToArray(), pickedFile.FileName, pickedFile.ContentType);
            MessageLabel.TextColor = result.Success ? Color.FromArgb("#8E2F18") : Color.FromArgb("#B91C1C");
            MessageLabel.Text = result.Message;

            if (result.Success)
            {
                LoadProfile();
            }
        }
        catch (Exception ex)
        {
            MessageLabel.TextColor = Color.FromArgb("#B91C1C");
            MessageLabel.Text = AppText.Get("Profile_UpdateAvatarFailed");
            Console.WriteLine($"Lỗi cập nhật avatar: {ex.Message}");
        }
    }

    private void OnResetMenuWelcomeImageClicked(object? sender, EventArgs e)
    {
        _authService.ClearMenuWelcomeImage();
        MessageLabel.TextColor = Color.FromArgb("#8E2F18");
        MessageLabel.Text = AppText.Get("Profile_MenuWelcomeImageReset");
        RefreshWelcomeBackgroundPreview();
    }

    private void OnWelcomePreviewHostSizeChanged(object? sender, EventArgs e)
    {
        if (!WelcomePreviewImage.IsVisible)
        {
            return;
        }

        ApplyWelcomeCropTransform();
    }

    private void RefreshWelcomeBackgroundPreview()
    {
        var (sourceValue, isBuiltInAsset) = _authService.GetMenuWelcomeImageSource();
        var hasBackground = !string.IsNullOrWhiteSpace(sourceValue) && (isBuiltInAsset || File.Exists(sourceValue));

        WelcomeBackgroundEditorBorder.IsVisible = _authService.CurrentUser is not null;
        if (!hasBackground)
        {
            WelcomePreviewImage.Source = null;
            WelcomePreviewImage.IsVisible = false;
            WelcomePreviewOverlay.IsVisible = false;
            WelcomePreviewImage.TranslationX = 0;
            WelcomePreviewImage.TranslationY = 0;
            WelcomePreviewImage.Scale = 1;
            return;
        }

        WelcomePreviewImage.Source = ImageSource.FromFile(sourceValue!);
        WelcomePreviewImage.IsVisible = true;
        WelcomePreviewOverlay.IsVisible = true;
        WelcomePreviewImage.Scale = WelcomePreviewScale;
        ApplyWelcomeCropTransform();
    }

    private void ApplyWelcomeCropTransform()
    {
        var width = WelcomePreviewHost.Width;
        var height = WelcomePreviewHost.Height;
        if (width <= 1 || height <= 1)
        {
            return;
        }

        var (offsetX, offsetY) = _authService.GetMenuWelcomeCropOffset();
        var maxX = width * WelcomePreviewPanFraction;
        var maxY = height * WelcomePreviewPanFraction;

        WelcomePreviewImage.TranslationX = maxX * offsetX;
        WelcomePreviewImage.TranslationY = maxY * offsetY;
    }

    private void OnWelcomePreviewPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (!WelcomePreviewImage.IsVisible)
        {
            return;
        }

        var maxX = WelcomePreviewHost.Width * WelcomePreviewPanFraction;
        var maxY = WelcomePreviewHost.Height * WelcomePreviewPanFraction;
        if (maxX <= 0 || maxY <= 0)
        {
            return;
        }

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _welcomePanStartX = WelcomePreviewImage.TranslationX;
                _welcomePanStartY = WelcomePreviewImage.TranslationY;
                break;

            case GestureStatus.Running:
                WelcomePreviewImage.TranslationX = Math.Clamp(_welcomePanStartX + e.TotalX, -maxX, maxX);
                WelcomePreviewImage.TranslationY = Math.Clamp(_welcomePanStartY + e.TotalY, -maxY, maxY);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                var normalizedX = (float)Math.Clamp(WelcomePreviewImage.TranslationX / maxX, -1d, 1d);
                var normalizedY = (float)Math.Clamp(WelcomePreviewImage.TranslationY / maxY, -1d, 1d);
                _authService.SetMenuWelcomeCropOffset(normalizedX, normalizedY);
                break;
        }
    }

    private void ApplyPresetBackground(string assetFileName)
    {
        var result = _authService.UseBuiltInMenuWelcomeImage(assetFileName);
        MessageLabel.TextColor = result.Success ? Color.FromArgb("#8E2F18") : Color.FromArgb("#B91C1C");
        MessageLabel.Text = result.Success
            ? AppText.Get("Profile_MenuWelcomePresetApplied")
            : result.Message;

        RefreshWelcomeBackgroundPreview();
    }

    private void OnApplyPresetAuroraTapped(object? sender, TappedEventArgs e)
    {
        ApplyPresetBackground("menu_welcome_bg_aurora.svg");
    }

    private void OnApplyPresetNeonGridTapped(object? sender, TappedEventArgs e)
    {
        ApplyPresetBackground("menu_welcome_bg_neon_grid.svg");
    }

    private void OnApplyPresetSunsetGlassTapped(object? sender, TappedEventArgs e)
    {
        ApplyPresetBackground("menu_welcome_bg_sunset_glass.svg");
    }

    private void OnApplyPresetSoftMeshTapped(object? sender, TappedEventArgs e)
    {
        ApplyPresetBackground("menu_welcome_bg_soft_mesh.svg");
    }

    private void OnApplyPresetOceanWaveTapped(object? sender, TappedEventArgs e)
    {
        ApplyPresetBackground("menu_welcome_bg_ocean_wave.svg");
    }

    private void OnApplyPresetUrbanNightTapped(object? sender, TappedEventArgs e)
    {
        ApplyPresetBackground("menu_welcome_bg_urban_night.svg");
    }

    private void OnApplyPresetMintFlowTapped(object? sender, TappedEventArgs e)
    {
        ApplyPresetBackground("menu_welcome_bg_mint_flow.svg");
    }

    private void OnApplyPresetRoseTechTapped(object? sender, TappedEventArgs e)
    {
        ApplyPresetBackground("menu_welcome_bg_rose_tech.svg");
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnSignOutClicked(object? sender, EventArgs e)
    {
        await _authService.SignOutAsync();
        await _navigator.ShowLoginAsync();
    }
}