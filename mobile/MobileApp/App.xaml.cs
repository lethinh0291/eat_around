using ZesTour.Views;

namespace MobileApp;

public partial class App : Application
{
	public App(MainPage mainPage)
	{
		InitializeComponent();

		// THAY VÌ: MainPage = mainPage;
		// NÍ SỬA THÀNH:
		MainPage = new NavigationPage(mainPage);

		// Nếu muốn thanh tiêu đề có màu xanh của shop ZESWAVE:
		// (Phải đặt sau khi gán MainPage)
		if (MainPage is NavigationPage navPage)
		{
			navPage.BarBackgroundColor = Color.FromArgb("#2196F3"); // Màu xanh
			navPage.BarTextColor = Colors.White; // Chữ trắng
		}
	}
}