namespace MobileApp;

using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Media;
public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
		TestGPS();
	}

	async void TestGPS()
	{
		var location = await Geolocation.GetLastKnownLocationAsync();

		if (location != null)
		{
			await DisplayAlert("GPS",
				$"Lat: {location.Latitude}, Lng: {location.Longitude}",
				"OK");
		}
		else
		{
			await DisplayAlert("GPS", "Không lấy được vị trí", "OK");
		}
	}
}
}
