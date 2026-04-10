using Microsoft.Extensions.DependencyInjection;
using ZesTour.Views;

namespace MobileApp;

public partial class App : Application
{
	private readonly IServiceProvider _services;

	public App(IServiceProvider services)
	{
		_services = services;
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var loadingPage = _services.GetRequiredService<LoadingPage>();
		return new Window(loadingPage);
	}
}