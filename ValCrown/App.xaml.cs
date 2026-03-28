using System.Windows;
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
        await StorageService.Init();
        CreateTray();

        var token = StorageService.Get("accessToken");
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
            _main.Closed += (s, ev) => { _main = null; };
        }
        _main.Show();
        _main.Activate();
    }

    private void CreateTray()
    {
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Text = "ValCrown",
            Visible = true
        };

        try
        {
            _tray.Icon = System.Drawing.SystemIcons.Application;
        }
        catch { }

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Open ValCrown", null, (s, ev) => ShowMain());
        menu.Items.Add("-");
        menu.Items.Add("Quit", null, (s, ev) => { _tray.Visible = false; Shutdown(); });
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (s, ev) => ShowMain();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        base.OnExit(e);
    }
}
