using Microsoft.Extensions.Logging;
using MobileApp.Services; // Nhớ thêm dòng này để nó nhận diện thư mục Services
using ZesTour.Views;

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

		// Đăng ký MainPage để hệ thống tự "bơm" (Inject) các Service vào Constructor
		builder.Services.AddSingleton<MainPage>();
		builder.Services.AddSingleton<LoadingPage>();

		// --- CẤU HÌNH DEBUG ---
#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}