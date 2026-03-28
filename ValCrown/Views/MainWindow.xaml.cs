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
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await WebView.EnsureCoreWebView2Async();
        var wv = WebView.CoreWebView2;
        wv.Settings.AreDefaultContextMenusEnabled = false;
        wv.Settings.AreDevToolsEnabled = false;
        wv.Settings.IsStatusBarEnabled = false;
        wv.WebMessageReceived += OnWebMessage;
        wv.Navigate("https://valcrown.com/app.html");
    }

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<BridgeMessage>(e.WebMessageAsJson);
            if (msg == null) return;
            Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    var result = await BridgeService.Handle(msg.Action, msg.Payload);
                    var rJson = JsonSerializer.Serialize(result);
                    var iJson = JsonSerializer.Serialize(msg.Id);
                    var script = "window.__bridgeCallback({id:" + iJson + ",result:" + rJson + "});";
                    await WebView.CoreWebView2.ExecuteScriptAsync(script);
                }
                catch { }
            });
        }
        catch { }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void MinBtn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaxBtn_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Hide();

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}

public class BridgeMessage
{
    public string Id { get; set; } = "";
    public string Action { get; set; } = "";
    public JsonElement Payload { get; set; }
}
