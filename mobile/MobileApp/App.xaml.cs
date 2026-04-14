using Microsoft.Extensions.DependencyInjection;
using MobileApp.Services;
using ZesTour.Views;

namespace MobileApp;

public partial class App : Application
{
	private readonly IServiceProvider _services;
	private readonly AppLanguageService _appLanguageService;

	public App(IServiceProvider services, AppLanguageService appLanguageService)
	{
		_services = services;
		_appLanguageService = appLanguageService;
		_appLanguageService.InitializeFromSettings();
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var loadingPage = _services.GetRequiredService<LoadingPage>();
		return new Window(loadingPage);
	}
}