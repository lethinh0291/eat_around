namespace ZesTour.Views;

public partial class LoadingPage : ContentPage
{
    private readonly MainPage _mainPage;
    private bool _hasNavigated;

    public LoadingPage(MainPage mainPage)
    {
        _mainPage = mainPage;
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
        await Task.Delay(1200);
        Window!.Page = new NavigationPage(_mainPage);
    }
}