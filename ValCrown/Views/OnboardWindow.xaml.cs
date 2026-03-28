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

        // Load onboard page - same auth.html from website but embedded
        wv.Navigate("https://valcrown.com/auth.html?mode=app");
    }

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<BridgeMessage>(e.WebMessageAsJson);
            if (msg?.Action == "auth.complete")
            {
                var payload = msg.Payload;
                StorageService.Set("accessToken", payload.TryGetString("accessToken"));
                StorageService.Set("refreshToken", payload.TryGetString("refreshToken"));
                StorageService.Set("user", payload.GetProperty("user").GetRawText());
                
                Dispatcher.Invoke(() =>
                {
                    ((App)Application.Current).ShowMain();
                    Close();
                });
            }
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
