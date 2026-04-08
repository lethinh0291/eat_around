using ZesTour.Views;

namespace MobileApp;

public partial class App : Application
{
	private readonly LoadingPage _loadingPage;

	public App(LoadingPage loadingPage)
	{
		InitializeComponent();
		_loadingPage = loadingPage;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(_loadingPage);
	}
}