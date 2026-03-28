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
        var wv = WebView.CoreWebView2;
        wv.Settings.AreDefaultContextMenusEnabled = false;
        wv.Settings.AreDevToolsEnabled = false;
        wv.WebMessageReceived += OnWebMessage;
        wv.Navigate("https://valcrown.com/auth.html");
    }

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<AuthMessage>(e.WebMessageAsJson);
            if (msg == null || msg.Action != "auth.complete") return;

            var token = msg.AccessToken ?? "";
            var refresh = msg.RefreshToken ?? "";
            var user = msg.UserJson ?? "{}";

            StorageService.Set("accessToken", token);
            StorageService.Set("refreshToken", refresh);
            StorageService.Set("user", user);

            Dispatcher.Invoke(() =>
            {
                ((App)Application.Current).ShowMain();
                Close();
            });
        }
        catch { }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}

public class AuthMessage
{
    public string Action { get; set; } = "";
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? UserJson { get; set; }
}
