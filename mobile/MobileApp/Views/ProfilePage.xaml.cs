using MobileApp.Services;
using MobileApp.Resources.Localization;
using System.IO;
using Microsoft.Maui.Storage;

namespace ZesTour.Views;

public partial class ProfilePage : ContentPage
{
    private readonly AuthService _authService;
    private readonly AppNavigator _navigator;

    public ProfilePage(AuthService authService, AppNavigator navigator)
    {
        _authService = authService;
        _navigator = navigator;
        InitializeComponent();
        ApplyLocalizedText();
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
        ChangeMenuWelcomeImageButton.Text = AppText.Get("Profile_ChangeMenuWelcomeImage");
        ResetMenuWelcomeImageButton.Text = AppText.Get("Profile_ResetMenuWelcomeImage");
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

    private async void OnChangeMenuWelcomeImageClicked(object? sender, EventArgs e)
    {
        MessageLabel.Text = string.Empty;

        var pickedFile = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = AppText.Get("Profile_PickMenuWelcomeImage"),
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

            var result = await _authService.UpdateMenuWelcomeImageAsync(memoryStream.ToArray(), pickedFile.FileName);
            MessageLabel.TextColor = result.Success ? Color.FromArgb("#8E2F18") : Color.FromArgb("#B91C1C");
            MessageLabel.Text = result.Success
                ? AppText.Get("Profile_MenuWelcomeImageUpdated")
                : AppText.Get("Profile_UpdateMenuWelcomeImageFailed");
        }
        catch
        {
            MessageLabel.TextColor = Color.FromArgb("#B91C1C");
            MessageLabel.Text = AppText.Get("Profile_UpdateMenuWelcomeImageFailed");
        }
    }

    private void OnResetMenuWelcomeImageClicked(object? sender, EventArgs e)
    {
        _authService.ClearMenuWelcomeImage();
        MessageLabel.TextColor = Color.FromArgb("#8E2F18");
        MessageLabel.Text = AppText.Get("Profile_MenuWelcomeImageReset");
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