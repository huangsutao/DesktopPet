using System.Windows;
using DesktopPet.UI;

namespace DesktopPet;

public partial class App : System.Windows.Application
{
    private TrayIconService? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow();
        _trayIcon = new TrayIconService(window);
        _trayIcon.Initialize();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
        base.OnExit(e);
    }
}
