using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using ValCrown.Services;
using System.IO;
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
        
        // Register C# → JS bridge
        wv.AddHostObjectToScript("nativeBridge", new NativeBridge(this));
        
        // Load app HTML from embedded resources
        var html = GetAppHtml();
        wv.NavigateToString(html);
        
        // Handle messages from JS
        wv.WebMessageReceived += OnWebMessage;
    }

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<BridgeMessage>(e.WebMessageAsJson);
            if (msg == null) return;
            
            Dispatcher.InvokeAsync(async () =>
            {
                var result = await BridgeService.Handle(msg.Action, msg.Payload);
                var js = $"window.__bridgeCallback({JsonSerializer.Serialize(new {{ id = msg.Id, result }})}); ";
                await WebView.CoreWebView2.ExecuteScriptAsync(js);
            });
        }
        catch { }
    }

    private static string GetAppHtml()
    {
        // Read from embedded resource
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("index.html"));
        if (name != null)
        {
            using var stream = asm.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        return "<html><body style='background:#07070f;color:white'>Loading...</body></html>";
    }

    // Window controls
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void MinBtn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaxBtn_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Hide(); // Minimize to tray
    }

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

[System.Runtime.InteropServices.ClassInterface(System.Runtime.InteropServices.ClassInterfaceType.AutoDual)]
[System.Runtime.InteropServices.ComVisible(true)]
public class NativeBridge
{
    private readonly MainWindow _win;
    public NativeBridge(MainWindow win) => _win = win;
}
