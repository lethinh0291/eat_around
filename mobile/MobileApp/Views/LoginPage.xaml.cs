using MobileApp.Services;
using MobileApp.Resources.Localization;

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
        ApplyLocalizedText();
    }

    private void ApplyLocalizedText()
    {
        WelcomeLabel.Text = AppText.Get("Login_WelcomeBack");
        ContinueDescriptionLabel.Text = AppText.Get("Login_ContinueDescription");
        AccountInfoLabel.Text = AppText.Get("Login_AccountInfo");
        IdentifierLabel.Text = AppText.Get("Login_IdentifierLabel");
        IdentifierEntry.Placeholder = AppText.Get("Login_IdentifierPlaceholder");
        PasswordLabel.Text = AppText.Get("Login_PasswordLabel");
        PasswordEntry.Placeholder = AppText.Get("Login_PasswordPlaceholder");
        RememberLabel.Text = AppText.Get("Login_Remember");
        LoginButton.Text = AppText.Get("Login_Button");
        CreateAccountButton.Text = AppText.Get("Login_CreateAccount");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_authService.CurrentUser is not null)
        {
            await _navigator.ShowMenuAsync();
        }
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
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

    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        await _navigator.ShowRegisterAsync();
    }
}
