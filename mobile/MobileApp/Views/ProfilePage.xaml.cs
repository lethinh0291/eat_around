using MobileApp.Services;
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
            NameLabel.Text = "Chưa đăng nhập";
            EmailLabel.Text = "Hãy đăng nhập để xem hồ sơ.";
            UsernameLabel.Text = "guest";
            UserIdLabel.Text = "#0000";
            StatusLabel.Text = "Không có phiên đăng nhập";
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
        StatusLabel.Text = "Đang hoạt động";
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
            PickerTitle = "Chọn ảnh đại diện",
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
            MessageLabel.Text = "Không thể cập nhật ảnh đại diện.";
            Console.WriteLine($"Lỗi cập nhật avatar: {ex.Message}");
        }
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