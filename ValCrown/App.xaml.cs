// Explicit aliases to resolve ambiguity between WPF and WinForms
using WpfApp = System.Windows.Application;
using WpfWindow = System.Windows.Window;
using WinFormsNotifyIcon = System.Windows.Forms.NotifyIcon;
using WinFormsContextMenu = System.Windows.Forms.ContextMenuStrip;
using System.Windows;
using ValCrown.Services;
using ValCrown.Views;

namespace ValCrown;

public partial class App : WpfApp
{
    private WinFormsNotifyIcon? _tray;
    private MainWindow? _main;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        await StorageService.Init();
        InitTray();

        var token = StorageService.Get("accessToken");
        if (string.IsNullOrWhiteSpace(token))
        {
            new OnboardWindow().Show();
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
            _main.Closed += (_, _) => _main = null;
        }
        _main.Show();
        _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    private void InitTray()
    {
        _tray = new WinFormsNotifyIcon
        {
            Text    = "ValCrown — Gaming Optimizer",
            Visible = true,
            Icon    = System.Drawing.SystemIcons.Application
        };

        var menu = new WinFormsContextMenu();
        menu.Items.Add("Open ValCrown", null, (_, _) => Dispatcher.Invoke(ShowMain));
        menu.Items.Add("-");
        menu.Items.Add("Quit", null, (_, _) => Dispatcher.Invoke(() =>
        {
            _tray!.Visible = false;
            Shutdown();
        }));

        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick     += (_, _) => Dispatcher.Invoke(ShowMain);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        base.OnExit(e);
    }
}
