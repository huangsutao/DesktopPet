using System.Windows;
using System.Windows.Threading;
using DesktopPet.Services;
using DesktopPet.UI;

namespace DesktopPet;

public partial class App : System.Windows.Application
{
    private static readonly TimeSpan MinSplashVisible = TimeSpan.FromSeconds(3);

    private TrayIconService? _trayIcon;
    private SettingsService? _settings;
    private SplashWindow? _splash;
    private MainWindow? _mainWindow;
    private DateTime _splashShownUtc;

    public static SettingsService Settings =>
        ((App)Current)._settings
        ?? throw new InvalidOperationException("Settings not initialized.");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 闪屏关闭时不要带崩整个应用；就绪后再绑到主窗
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _splash = new SplashWindow();
        _splashShownUtc = DateTime.UtcNow;
        _splash.PlaceAtBottomRight();
        _splash.Show();
        _splash.PlaceAtBottomRight();
        DoEvents();

        Dispatcher.BeginInvoke(ContinueStartup, DispatcherPriority.Loaded);
    }

    private void ContinueStartup()
    {
        _settings = new SettingsService();
        _settings.Load();
        LocalizationService.Instance.Initialize(_settings.Config.UiLanguage);

        _mainWindow = new MainWindow();
        _mainWindow.AttachSettings(_settings);
        _mainWindow.Topmost = false;
        _mainWindow.Opacity = 0;
        MainWindow = _mainWindow;

        _trayIcon = new TrayIconService(_mainWindow, _settings);
        _trayIcon.Initialize();

        _mainWindow.Ready += OnMainWindowReady;
        _mainWindow.Show();
        _splash?.Activate();
    }

    private async void OnMainWindowReady()
    {
        if (_mainWindow is not null)
        {
            _mainWindow.Ready -= OnMainWindowReady;
        }

        // 不足 3 秒则补足；超过则立刻关
        var remain = MinSplashVisible - (DateTime.UtcNow - _splashShownUtc);
        if (remain > TimeSpan.Zero)
        {
            await Task.Delay(remain).ConfigureAwait(true);
        }

        var splash = _splash;
        _splash = null;
        splash?.Close();

        if (_mainWindow is null)
        {
            Shutdown();
            return;
        }

        _mainWindow.Opacity = 1;
        _mainWindow.Topmost = _settings?.Config.Topmost ?? true;
        _mainWindow.Activate();

        ShutdownMode = ShutdownMode.OnMainWindowClose;
        MainWindow = _mainWindow;
    }

    private static void DoEvents()
    {
        Current.Dispatcher.Invoke(DispatcherPriority.Render, static () => { });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
        base.OnExit(e);
    }
}
