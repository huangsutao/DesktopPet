using System.Windows;
using DesktopPet.Services;
using DesktopPet.UI;

namespace DesktopPet;

public partial class App : System.Windows.Application
{
    private TrayIconService? _trayIcon;
    private SettingsService? _settings;

    public static SettingsService Settings =>
        ((App)Current)._settings
        ?? throw new InvalidOperationException("Settings not initialized.");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settings = new SettingsService();
        _settings.Load();

        LocalizationService.Instance.Initialize(_settings.Config.UiLanguage);

        var window = new MainWindow();
        window.AttachSettings(_settings);
        window.Topmost = _settings.Config.Topmost;

        _trayIcon = new TrayIconService(window, _settings);
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
