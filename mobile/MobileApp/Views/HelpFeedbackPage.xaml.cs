using System.Text.Json;
using Microsoft.Maui.Storage;

namespace ZesTour.Views;

public partial class HelpFeedbackPage : ContentPage
{
    private const string FeedbackKey = "zes_feedback_v1";

    public HelpFeedbackPage()
    {
        InitializeComponent();
    }

    private async void OnSubmitClicked(object? sender, EventArgs e)
    {
        MessageLabel.Text = string.Empty;

        var email = EmailEntry.Text?.Trim() ?? string.Empty;
        var title = TitleEntry.Text?.Trim() ?? string.Empty;
        var content = ContentEditor.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
        {
            MessageLabel.Text = "Vui lòng nhập tiêu đề và nội dung phản hồi.";
            return;
        }

        var items = LoadItems();
        items.Add(new FeedbackItem
        {
            Email = email,
            Title = title,
            Content = content,
            CreatedAtUtc = DateTime.UtcNow
        });

        Preferences.Default.Set(FeedbackKey, JsonSerializer.Serialize(items));
        await DisplayAlertAsync("Cảm ơn", "Phản hồi của bạn đã được ghi nhận.", "OK");

        EmailEntry.Text = string.Empty;
        TitleEntry.Text = string.Empty;
        ContentEditor.Text = string.Empty;
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        await Navigation.PopAsync();
    }

    private static List<FeedbackItem> LoadItems()
    {
        var json = Preferences.Default.Get(FeedbackKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<FeedbackItem>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<FeedbackItem>>(json) ?? new List<FeedbackItem>();
        }
        catch
        {
            return new List<FeedbackItem>();
        }
    }

    private sealed class FeedbackItem
    {
        public string Email { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }
}
