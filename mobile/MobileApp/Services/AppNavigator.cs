using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using ZesTour.Views;

namespace MobileApp.Services;

public class AppNavigator
{
    private readonly IServiceProvider _services;

    public AppNavigator(IServiceProvider services)
    {
        _services = services;
    }

    public Task ShowLoginAsync()
    {
        return SetRootWithNavigationAsync<LoginPage>();
    }

    public Task ShowMainAsync()
    {
        return SetRootWithNavigationAsync<MainPage>();
    }

    public Task ShowMenuAsync()
    {
        return SetRootWithNavigationAsync<MenuPage>();
    }

    public Task ShowLoadingAsync()
    {
        SetRoot(_services.GetRequiredService<LoadingPage>());
        return Task.CompletedTask;
    }

    public async Task ShowMainFromMenuAsync()
    {
        await PushOnNavigationAsync<MainPage>(ShowMainAsync);
    }

    public async Task ShowRegisterAsync()
    {
        await PushOnNavigationAsync<RegisterPage>(ShowLoginAsync);
    }

    public async Task ShowProfileAsync()
    {
        await PushOnNavigationAsync<ProfilePage>(ShowMenuAsync);
    }

    public async Task ShowStoreRegistrationAsync()
    {
        await PushOnNavigationAsync<StoreRegistrationPage>(ShowMenuAsync);
    }

    public async Task ShowStoreManagementAsync()
    {
        await PushOnNavigationAsync<StoreManagementPage>(ShowMenuAsync);
    }

    public async Task ShowMyTripsAsync()
    {
        await PushOnNavigationAsync<MyTripsPage>(ShowMenuAsync);
    }

    public async Task ShowSettingsAsync()
    {
        await PushOnNavigationAsync<SettingsPage>(ShowMenuAsync);
    }

    public async Task ShowHelpFeedbackAsync()
    {
        await PushOnNavigationAsync<HelpFeedbackPage>(ShowMenuAsync);
    }

    public async Task ShowQrTriggerAsync()
    {
        await PushOnNavigationAsync<QrTriggerPage>(ShowMenuAsync);
    }

    private Task SetRootWithNavigationAsync<TPage>() where TPage : Page
    {
        SetRoot(new NavigationPage(_services.GetRequiredService<TPage>()));
        return Task.CompletedTask;
    }

    private async Task PushOnNavigationAsync<TPage>(Func<Task> ensureRootAsync) where TPage : Page
    {
        var navigation = EnsureNavigationPage();
        if (navigation is null)
        {
            await ensureRootAsync();
            navigation = EnsureNavigationPage();
        }

        if (navigation is not null)
        {
            await navigation.PushAsync(_services.GetRequiredService<TPage>());
        }
    }

    private void SetRoot(Page page)
    {
        if (Application.Current is null)
        {
            MainThread.BeginInvokeOnMainThread(() => SetRoot(page));
            return;
        }

        var window = Application.Current.Windows.FirstOrDefault();
        if (window is not null)
        {
            window.Page = page;
        }
        else
        {
            // Queue it if window isn't ready yet
            MainThread.BeginInvokeOnMainThread(() => SetRoot(page));
        }
    }

    private NavigationPage? EnsureNavigationPage()
    {
        var window = Application.Current?.Windows.FirstOrDefault();
        return window?.Page as NavigationPage;
    }
}