using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using ValCrown.Services;
using System.Text.Json;
using System.IO;

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

        // Load embedded app.html — fully self-contained, no internet needed for UI
        var html = LoadEmbeddedHtml("app.html");
        core.NavigateToString(html);
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<BridgeMessage>(
                e.WebMessageAsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (msg is null) return;

            // Window actions handled on UI thread
            if (msg.Action == "window.minimize") { Dispatcher.Invoke(() => WindowState = WindowState.Minimized); return; }
            if (msg.Action == "window.maximize") { Dispatcher.Invoke(() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized); return; }
            if (msg.Action == "window.hide")     { Dispatcher.Invoke(Hide); return; }

            // Auth complete — open main window (onboard only)
            if (msg.Action == "auth.complete")
            {
                try
                {
                    StorageService.Set("accessToken",  msg.Payload.TryGetProperty("accessToken",  out var at)  ? at.GetString()  : "");
                    StorageService.Set("refreshToken", msg.Payload.TryGetProperty("refreshToken", out var rt)  ? rt.GetString()  : "");
                    StorageService.Set("user",         msg.Payload.TryGetProperty("user",         out var usr) ? usr.GetRawText() : "{}");
                }
                catch { }
                return;
            }

            // All other actions — handle on background thread
            Task.Run(async () =>
            {
                try
                {
                    var result = await BridgeService.Handle(msg.Action, msg.Payload);
                    var rJson  = JsonSerializer.Serialize(result);
                    var idJson = JsonSerializer.Serialize(msg.Id);
                    var script = "window.__bridgeCallback({id:" + idJson + ",result:" + rJson + "});";

                    await Dispatcher.InvokeAsync(async () =>
                    {
                        if (WebView?.CoreWebView2 != null)
                            await WebView.CoreWebView2.ExecuteScriptAsync(script);
                    });
                }
                catch { }
            });
        }
        catch { }
    }

    private static string LoadEmbeddedHtml(string filename)
    {
        // Try load from Assets folder next to exe
        var exeDir  = System.AppContext.BaseDirectory;
        var assetPath = Path.Combine(exeDir, "Assets", filename);
        if (File.Exists(assetPath))
            return File.ReadAllText(assetPath);

        // Fallback: load from same directory
        var localPath = Path.Combine(exeDir, filename);
        if (File.Exists(localPath))
            return File.ReadAllText(localPath);

        return "<html><body style='background:#07070f;color:#f0f0ff;font-family:sans-serif;padding:40px'><h2>ValCrown</h2><p>Loading...</p></body></html>";
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void MinBtn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaxBtn_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Hide();

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}

public sealed class BridgeMessage
{
    public string      Id      { get; set; } = string.Empty;
    public string      Action  { get; set; } = string.Empty;
    public JsonElement Payload { get; set; }
}
