using MobileApp.Services;

namespace ZesTour.Views;

public partial class StoreManagementPage : ContentPage
{
    private readonly ApiService _apiService;
    private readonly AuthService _authService;
    private List<ApiService.ManagementStoreRegistration> _items = new();
    private ApiService.ManagementStoreRegistration? _selected;

    public StoreManagementPage(ApiService apiService, AuthService authService)
    {
        _apiService = apiService;
        _authService = authService;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!string.Equals(_authService.CurrentUser?.Role, "seller", StringComparison.OrdinalIgnoreCase))
        {
            await DisplayAlertAsync("Không có quyền", "Chỉ tài khoản Người bán mới có thể quản lý cửa hàng.", "OK");
            await Navigation.PopAsync();
            return;
        }

        await LoadMyStoresAsync();
    }

    private async Task LoadMyStoresAsync()
    {
        StatusLabel.Text = string.Empty;
        var ownerName = _authService.CurrentUser?.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(ownerName))
        {
            StatusLabel.Text = "Không xác định được chủ cửa hàng hiện tại.";
            RegistrationsCollection.ItemsSource = null;
            return;
        }

        _items = await _apiService.GetMyStoreRegistrationsAsync(ownerName);
        RegistrationsCollection.ItemsSource = _items;

        if (_items.Count == 0)
        {
            StatusLabel.Text = "Bạn chưa có đăng ký cửa hàng nào.";
            ClearEditor();
            return;
        }

        _selected = _items[0];
        RegistrationsCollection.SelectedItem = _selected;
        FillEditor(_selected);
    }

    private void FillEditor(ApiService.ManagementStoreRegistration item)
    {
        StoreNameEntry.Text = item.StoreName;
        PhoneEntry.Text = item.Phone;
        AddressEntry.Text = item.Address;
        CategoryEntry.Text = item.Category;
        DescriptionEditor.Text = item.Description;
    }

    private void ClearEditor()
    {
        _selected = null;
        StoreNameEntry.Text = string.Empty;
        PhoneEntry.Text = string.Empty;
        AddressEntry.Text = string.Empty;
        CategoryEntry.Text = string.Empty;
        DescriptionEditor.Text = string.Empty;
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        await LoadMyStoresAsync();
    }

    private void OnRegistrationSelected(object? sender, SelectionChangedEventArgs e)
    {
        var item = e.CurrentSelection?.FirstOrDefault() as ApiService.ManagementStoreRegistration;
        if (item is null)
        {
            return;
        }

        _selected = item;
        FillEditor(item);
    }

    private async void OnUpdateClicked(object? sender, EventArgs e)
    {
        StatusLabel.Text = string.Empty;

        if (_selected is null)
        {
            StatusLabel.Text = "Vui lòng chọn một đăng ký để cập nhật.";
            return;
        }

        var ownerName = _authService.CurrentUser?.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(ownerName))
        {
            StatusLabel.Text = "Không xác định được chủ cửa hàng hiện tại.";
            return;
        }

        var storeName = StoreNameEntry.Text?.Trim() ?? string.Empty;
        var phone = PhoneEntry.Text?.Trim() ?? string.Empty;
        var address = AddressEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(storeName) || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(address))
        {
            StatusLabel.Text = "Tên cửa hàng, số điện thoại và địa chỉ là bắt buộc.";
            return;
        }

        _selected.StoreName = storeName;
        _selected.Phone = phone;
        _selected.Address = address;
        _selected.Category = CategoryEntry.Text?.Trim() ?? string.Empty;
        _selected.Description = DescriptionEditor.Text?.Trim() ?? string.Empty;

        var result = await _apiService.UpdateMyStoreRegistrationAsync(_selected, ownerName);
        StatusLabel.TextColor = result.Success ? Color.FromArgb("#166534") : Color.FromArgb("#B91C1C");
        StatusLabel.Text = result.Message;
        if (result.Success)
        {
            await LoadMyStoresAsync();
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        StatusLabel.Text = string.Empty;

        if (_selected is null)
        {
            StatusLabel.Text = "Vui lòng chọn một đăng ký để xóa.";
            return;
        }

        var confirm = await DisplayAlertAsync("Xác nhận", "Bạn có chắc muốn xóa đăng ký này?", "Xóa", "Hủy");
        if (!confirm)
        {
            return;
        }

        var ownerName = _authService.CurrentUser?.Name?.Trim() ?? string.Empty;
        var result = await _apiService.DeleteMyStoreRegistrationAsync(_selected.Id, ownerName);
        StatusLabel.TextColor = result.Success ? Color.FromArgb("#166534") : Color.FromArgb("#B91C1C");
        StatusLabel.Text = result.Message;

        if (result.Success)
        {
            await LoadMyStoresAsync();
        }
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        await Navigation.PopAsync();
    }
}
