using MobileApp.Services;
using MobileApp.Resources.Localization;

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
        ApplyLocalizedText();
        UpdateSelectedRoleText();
    }

    private void ApplyLocalizedText()
    {
        RegisterTitleLabel.Text = AppText.Get("Register_Title");
        RegisterSubtitleLabel.Text = AppText.Get("Register_Subtitle");
        NameTitleLabel.Text = AppText.Get("Register_Name");
        NameEntry.Placeholder = AppText.Get("Register_NamePlaceholder");
        UsernameTitleLabel.Text = AppText.Get("Register_Username");
        UsernameEntry.Placeholder = AppText.Get("Register_UsernamePlaceholder");
        EmailTitleLabel.Text = AppText.Get("Register_Email");
        EmailEntry.Placeholder = AppText.Get("Register_EmailPlaceholder");
        PasswordTitleLabel.Text = AppText.Get("Register_Password");
        PasswordEntry.Placeholder = AppText.Get("Register_PasswordPlaceholder");
        ConfirmPasswordTitleLabel.Text = AppText.Get("Register_ConfirmPassword");
        ConfirmPasswordEntry.Placeholder = AppText.Get("Register_ConfirmPasswordPlaceholder");
        SellerAccountTitleLabel.Text = AppText.Get("Register_SellerTitle");
        SellerAccountHintLabel.Text = AppText.Get("Register_SellerHint");
        RegisterButton.Text = AppText.Get("Register_Submit");
        BackToLoginButton.Text = AppText.Get("Register_BackToLogin");
    }

    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        MessageLabel.Text = string.Empty;

        var password = PasswordEntry.Text ?? string.Empty;
        var confirmPassword = ConfirmPasswordEntry.Text ?? string.Empty;

        if (!password.Equals(confirmPassword, StringComparison.Ordinal))
        {
            MessageLabel.Text = AppText.Get("Register_PasswordMismatch");
            return;
        }

        var result = await _authService.RegisterAsync(
            NameEntry.Text ?? string.Empty,
            UsernameEntry.Text ?? string.Empty,
            EmailEntry.Text ?? string.Empty,
            password,
            registerAsSeller: SellerSwitch.IsToggled);

        if (!result.Success)
        {
            MessageLabel.Text = result.Message;
            return;
        }

        await DisplayAlertAsync(AppText.Get("Register_AlertTitle"), result.Message, AppText.Get("Common_Ok"));
        await Navigation.PopAsync();
    }

    private async void OnBackToLoginClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private void OnSellerSwitchToggled(object? sender, ToggledEventArgs e)
    {
        UpdateSelectedRoleText();
    }

    private void UpdateSelectedRoleText()
    {
        SelectedRoleLabel.Text = SellerSwitch.IsToggled
            ? AppText.Get("Register_RoleSeller")
            : AppText.Get("Register_RoleCustomer");
    }
}