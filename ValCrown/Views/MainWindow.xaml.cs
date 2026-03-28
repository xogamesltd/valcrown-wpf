using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using ValCrown.Services;
using System.Text.Json;

namespace ValCrown.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded  += OnLoaded;
        Closing += OnWindowClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await WebView.EnsureCoreWebView2Async();

        var core = WebView.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled            = false;
        core.Settings.IsStatusBarEnabled            = false;
        core.Settings.IsZoomControlEnabled          = false;
        core.WebMessageReceived += OnWebMessageReceived;

        // Load the ValCrown web app
        core.Navigate("https://valcrown.com/app.html");
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<BridgeMessage>(
                e.WebMessageAsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (msg is null) return;

            // Handle on background thread, callback on UI thread
            Task.Run(async () =>
            {
                try
                {
                    var result  = await BridgeService.Handle(msg.Action, msg.Payload);
                    var rJson   = JsonSerializer.Serialize(result);
                    var idJson  = JsonSerializer.Serialize(msg.Id);
                    var script  = "window.__bridgeCallback({id:" + idJson + ",result:" + rJson + "});";

                    await Dispatcher.InvokeAsync(async () =>
                    {
                        if (WebView?.CoreWebView2 != null)
                            await WebView.CoreWebView2.ExecuteScriptAsync(script);
                    });
                }
                catch { /* silent — never crash the app */ }
            });
        }
        catch { }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void MinBtn_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaxBtn_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
        => Hide(); // Minimize to tray, don't close

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Intercept close — minimize to tray instead
        e.Cancel = true;
        Hide();
    }
}

/// <summary>Message contract from JS bridge.</summary>
public sealed class BridgeMessage
{
    public string      Id      { get; set; } = string.Empty;
    public string      Action  { get; set; } = string.Empty;
    public JsonElement Payload { get; set; }
}
