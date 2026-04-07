using Microsoft.Extensions.Logging;

namespace MobileApp;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
		// Trong file MauiProgram.cs
		builder.Services.AddSingleton<DatabaseService>();
		builder.Services.AddSingleton<ApiService>();
		builder.Services.AddSingleton<LocationService>();
		builder.Services.AddTransient<MainPage>();
#endif

		return builder.Build();
	}
}
