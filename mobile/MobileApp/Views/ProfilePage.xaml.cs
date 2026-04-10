using MobileApp.Services;

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
            InitialsLabel.Text = "U";
            StatusLabel.Text = "Không có phiên đăng nhập";
            return;
        }

        NameLabel.Text = user.Name;
        EmailLabel.Text = user.Email;
        UsernameLabel.Text = user.Username;
        UserIdLabel.Text = $"#{user.Id:0000}";
        InitialsLabel.Text = _authService.GetInitials(user);
        StatusLabel.Text = "Đang hoạt động";
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