using Microsoft.Extensions.Logging;
using MobileApp.Services; // Nhớ thêm dòng này để nó nhận diện thư mục Services
using ZesTour.Views;
using Microsoft.Extensions.DependencyInjection;

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

		// --- ĐĂNG KÝ CÁC DỊCH VỤ (SERVICES) Ở ĐÂY ---

		// Dùng Singleton cho các Service chạy suốt vòng đời App
		builder.Services.AddSingleton<DatabaseService>();
		builder.Services.AddSingleton<ApiService>();
		builder.Services.AddSingleton<LocationService>();
		builder.Services.AddSingleton<AuthService>();
		builder.Services.AddSingleton<AppNavigator>();

		// Đăng ký các Page theo kiểu transient để mỗi lần điều hướng là một instance sạch
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<MenuPage>();
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<RegisterPage>();
		builder.Services.AddTransient<StoreRegistrationPage>();
		builder.Services.AddTransient<MyTripsPage>();
		builder.Services.AddTransient<SettingsPage>();
		builder.Services.AddTransient<HelpFeedbackPage>();
		builder.Services.AddTransient<ProfilePage>();
		builder.Services.AddTransient<LoadingPage>();

		// --- CẤU HÌNH DEBUG ---
#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}