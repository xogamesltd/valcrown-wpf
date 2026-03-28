using System.Windows;
using System.IO;
using ValCrown.Services;
using ValCrown.Views;

namespace ValCrown;

public partial class App : Application
{
    private System.Windows.Forms.NotifyIcon? _tray;
    private MainWindow? _main;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Init services
        await StorageService.Init();
        
        var token = StorageService.Get("accessToken");
        
        // Create tray icon
        CreateTray();

        if (string.IsNullOrEmpty(token))
        {
            var onboard = new OnboardWindow();
            onboard.Show();
        }
        else
        {
            ShowMain();
        }
    }

    public void ShowMain()
    {
        if (_main == null || !_main.IsLoaded)
        {
            _main = new MainWindow();
            _main.Closed += (s, e) => { _main = null; };
        }
        _main.Show();
        _main.Activate();
    }

    private void CreateTray()
    {
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Text = "ValCrown — Gaming Optimizer",
            Visible = true,
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName)
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Open ValCrown", null, (s, e) => ShowMain());
        menu.Items.Add("-");
        menu.Items.Add("Quit", null, (s, e) => { _tray.Visible = false; Shutdown(); });
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (s, e) => ShowMain();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        base.OnExit(e);
    }
}
