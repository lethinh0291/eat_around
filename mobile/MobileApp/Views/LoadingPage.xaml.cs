using MobileApp.Services;
using MobileApp.Resources.Localization;

namespace ZesTour.Views;

public partial class LoadingPage : ContentPage
{
    private readonly AppNavigator _navigator;
    private readonly AuthService _authService;
    private bool _hasNavigated;

    public LoadingPage(AppNavigator navigator, AuthService authService)
    {
        _navigator = navigator;
        _authService = authService;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_hasNavigated)
        {
            return;
        }

        _hasNavigated = true;
        await Task.Delay(650);

        try
        {
            if (_authService.CurrentUser is not null)
            {
                await _navigator.ShowMenuAsync();
                return;
            }

            await _navigator.ShowLoginAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation error: {ex}");
            await DisplayAlertAsync(
                AppText.Get("Loading_ErrorTitle"),
                AppText.Format("Loading_StartupError", ex.Message),
                AppText.Get("Common_Ok"));
        }
    }
}