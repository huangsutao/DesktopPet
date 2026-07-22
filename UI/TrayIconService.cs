using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace DesktopPet.UI;

/// <summary>
/// System tray icon: show/hide pet window and exit application.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly Window _window;
    private NotifyIcon? _notifyIcon;
    private Icon? _icon;

    public TrayIconService(Window window)
    {
        _window = window;
    }

    public void Initialize()
    {
        if (_notifyIcon is not null)
        {
            return;
        }

        _icon = LoadTrayIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "DesktopPet",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };

        _notifyIcon.DoubleClick += (_, _) => ShowWindow();
    }

    public void Dispose()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _icon?.Dispose();
        _icon = null;
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("显示", null, (_, _) => ShowWindow());
        menu.Items.Add("隐藏", null, (_, _) => HideWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitApp());
        return menu;
    }

    private void ShowWindow()
    {
        _window.Show();
        _window.Activate();
        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }
    }

    private void HideWindow()
    {
        _window.Hide();
    }

    private static void ExitApp()
    {
        Application.Current.Shutdown();
    }

    private static Icon LoadTrayIcon()
    {
        var pngPath = Path.Combine(AppContext.BaseDirectory, "Resources", "tray-pet.png");
        if (!File.Exists(pngPath))
        {
            return SystemIcons.Application;
        }

        using var bitmap = new Bitmap(pngPath);
        var handle = bitmap.GetHicon();
        using var temp = Icon.FromHandle(handle);
        return (Icon)temp.Clone();
    }
}
