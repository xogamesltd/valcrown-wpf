using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using ValCrown.Services;
using System.Text.Json;

namespace ValCrown.Views;

public partial class OnboardWindow : Window
{
    public OnboardWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await WebView.EnsureCoreWebView2Async();

        var core = WebView.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled            = false;
        core.WebMessageReceived += OnWebMessageReceived;

        // Load the auth page from website
        core.Navigate("https://valcrown.com/auth.html");
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<OnboardMessage>(
                e.WebMessageAsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (msg is null || msg.Action != "auth.complete") return;

            StorageService.Set("accessToken",  msg.AccessToken  ?? string.Empty);
            StorageService.Set("refreshToken", msg.RefreshToken ?? string.Empty);
            StorageService.Set("user",         msg.UserJson     ?? "{}");

            Dispatcher.Invoke(() =>
            {
                (System.Windows.Application.Current as App)?.ShowMain();
                Close();
            });
        }
        catch { }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
        => System.Windows.Application.Current.Shutdown();
}

public sealed class OnboardMessage
{
    public string  Action        { get; set; } = string.Empty;
    public string? AccessToken   { get; set; }
    public string? RefreshToken  { get; set; }
    public string? UserJson      { get; set; }
}
