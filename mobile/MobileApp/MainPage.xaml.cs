using MobileApp.Services;
using SharedLib.Models;

namespace MobileApp;

public partial class MainPage : ContentPage
{
	private readonly DatabaseService _dbService;
	private readonly ApiService _apiService;

	public MainPage(DatabaseService dbService, ApiService apiService)
	{
		InitializeComponent();
		_dbService = dbService;
		_apiService = apiService;
	}

	// Bước 1: Kéo dữ liệu từ Backend về máy
	private async void OnSyncDataClicked(object sender, EventArgs e)
	{
		StatusLabel.Text = "Đang tải dữ liệu...";
		var poisFromServer = await _apiService.GetPoisAsync();

		if (poisFromServer.Any())
		{
			await _dbService.SavePoisAsync(poisFromServer);
			await DisplayAlert("Thành công", $"Đã lưu {poisFromServer.Count} điểm POI offline!", "OK");
			StatusLabel.Text = "Đã đồng bộ xong!";
		}
		else
		{
			await DisplayAlert("Lỗi", "Không lấy được dữ liệu. Kiểm tra Backend hoặc mạng nhé bạn!", "OK");
		}
	}

	// Bước 2: Chạy thử tính năng thuyết minh (TTS)
	private async void OnTestVoiceClicked(object sender, EventArgs e)
	{
		// Lấy thử 1 điểm từ SQLite ra nói
		var pois = await _dbService.GetPOIsAsync();
		if (pois.Any())
		{
			var firstPoi = pois.First();
			await TextToSpeech.Default.SpeakAsync($"Bạn đang tới gần {firstPoi.Name}. {firstPoi.Description}");
		}
		else
		{
			await TextToSpeech.Default.SpeakAsync("Chưa có dữ liệu bạn ơi, bấm đồng bộ đi!");
		}
	}
}