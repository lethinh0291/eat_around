using MobileApp.Services;

namespace ZesTour.Views;

public partial class LoginPage : ContentPage
{
    private readonly AuthService _authService;
    private readonly AppNavigator _navigator;

    public LoginPage(AuthService authService, AppNavigator navigator)
    {
        _authService = authService;
        _navigator = navigator;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_authService.CurrentUser is not null)
        {
            await _navigator.ShowMenuAsync();
        }
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        MessageLabel.Text = string.Empty;

        var result = await _authService.LoginAsync(
            IdentifierEntry.Text ?? string.Empty,
            PasswordEntry.Text ?? string.Empty);

        if (!result.Success)
        {
            MessageLabel.Text = result.Message;
            return;
        }

        await _navigator.ShowMenuAsync();
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        await _navigator.ShowRegisterAsync();
    }
}
