using MobileApp.Services;

namespace ZesTour.Views;

public partial class RegisterPage : ContentPage
{
    private readonly AuthService _authService;
    private readonly AppNavigator _navigator;

    public RegisterPage(AuthService authService, AppNavigator navigator)
    {
        _authService = authService;
        _navigator = navigator;
        InitializeComponent();
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        MessageLabel.Text = string.Empty;

        var password = PasswordEntry.Text ?? string.Empty;
        var confirmPassword = ConfirmPasswordEntry.Text ?? string.Empty;

        if (!password.Equals(confirmPassword, StringComparison.Ordinal))
        {
            MessageLabel.Text = "Mật khẩu xác nhận không khớp.";
            return;
        }

        var result = await _authService.RegisterAsync(
            NameEntry.Text ?? string.Empty,
            UsernameEntry.Text ?? string.Empty,
            EmailEntry.Text ?? string.Empty,
            password);

        if (!result.Success)
        {
            MessageLabel.Text = result.Message;
            return;
        }

        await DisplayAlertAsync("Đăng ký", result.Message, "OK");
        await Navigation.PopAsync();
    }

    private async void OnBackToLoginClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}