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
        SetRoot(new NavigationPage(_services.GetRequiredService<LoginPage>()));
        return Task.CompletedTask;
    }

    public Task ShowMainAsync()
    {
        SetRoot(new NavigationPage(_services.GetRequiredService<MainPage>()));
        return Task.CompletedTask;
    }

    public Task ShowMenuAsync()
    {
        SetRoot(new NavigationPage(_services.GetRequiredService<MenuPage>()));
        return Task.CompletedTask;
    }

    public Task ShowLoadingAsync()
    {
        SetRoot(_services.GetRequiredService<LoadingPage>());
        return Task.CompletedTask;
    }

    public async Task ShowMainFromMenuAsync()
    {
        var navigation = EnsureNavigationPage();
        if (navigation is null)
        {
            await ShowMainAsync();
            return;
        }

        await navigation.PushAsync(_services.GetRequiredService<MainPage>());
    }

    public async Task ShowRegisterAsync()
    {
        var navigation = EnsureNavigationPage();
        if (navigation is null)
        {
            await ShowLoginAsync();
            navigation = EnsureNavigationPage();
        }

        if (navigation is not null)
        {
            await navigation.PushAsync(_services.GetRequiredService<RegisterPage>());
        }
    }

    public async Task ShowProfileAsync()
    {
        var navigation = EnsureNavigationPage();
        if (navigation is null)
        {
            await ShowMenuAsync();
            navigation = EnsureNavigationPage();
        }

        if (navigation is not null)
        {
            await navigation.PushAsync(_services.GetRequiredService<ProfilePage>());
        }
    }

    public async Task ShowStoreRegistrationAsync()
    {
        var navigation = EnsureNavigationPage();
        if (navigation is null)
        {
            await ShowMenuAsync();
            navigation = EnsureNavigationPage();
        }

        if (navigation is not null)
        {
            await navigation.PushAsync(_services.GetRequiredService<StoreRegistrationPage>());
        }
    }

    public async Task ShowStoreManagementAsync()
    {
        var navigation = EnsureNavigationPage();
        if (navigation is null)
        {
            await ShowMenuAsync();
            navigation = EnsureNavigationPage();
        }

        if (navigation is not null)
        {
            await navigation.PushAsync(_services.GetRequiredService<StoreManagementPage>());
        }
    }

    public async Task ShowMyTripsAsync()
    {
        var navigation = EnsureNavigationPage();
        if (navigation is null)
        {
            await ShowMenuAsync();
            navigation = EnsureNavigationPage();
        }

        if (navigation is not null)
        {
            await navigation.PushAsync(_services.GetRequiredService<MyTripsPage>());
        }
    }

    public async Task ShowSettingsAsync()
    {
        var navigation = EnsureNavigationPage();
        if (navigation is null)
        {
            await ShowMenuAsync();
            navigation = EnsureNavigationPage();
        }

        if (navigation is not null)
        {
            await navigation.PushAsync(_services.GetRequiredService<SettingsPage>());
        }
    }

    public async Task ShowHelpFeedbackAsync()
    {
        var navigation = EnsureNavigationPage();
        if (navigation is null)
        {
            await ShowMenuAsync();
            navigation = EnsureNavigationPage();
        }

        if (navigation is not null)
        {
            await navigation.PushAsync(_services.GetRequiredService<HelpFeedbackPage>());
        }
    }

    public async Task ShowQrTriggerAsync()
    {
        var navigation = EnsureNavigationPage();
        if (navigation is null)
        {
            await ShowMenuAsync();
            navigation = EnsureNavigationPage();
        }

        if (navigation is not null)
        {
            await navigation.PushAsync(_services.GetRequiredService<QrTriggerPage>());
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