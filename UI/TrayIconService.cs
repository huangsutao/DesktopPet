using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using DesktopPet.Services;
using Application = System.Windows.Application;

namespace DesktopPet.UI;

/// <summary>
/// System tray icon: show/hide, settings, switch pet, about, exit.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly Window _window;
    private readonly SettingsService _settings;
    private NotifyIcon? _notifyIcon;
    private Icon? _icon;
    private ToolStripMenuItem? _switchPetMenu;
    private ToolStripMenuItem? _clickThroughMenu;

    /// <summary>True while the tray context menu is visible.</summary>
    public static bool IsMenuOpen { get; private set; }

    public TrayIconService(Window window, SettingsService settings)
    {
        _window = window;
        _settings = settings;
        _settings.Changed += OnSettingsChanged;
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
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
            Text = BuildTrayText(),
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };

        _notifyIcon.DoubleClick += (_, _) => ShowWindow();
    }

    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;

        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            IsMenuOpen = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _icon?.Dispose();
        _icon = null;
    }

    private void OnLanguageChanged()
    {
        var app = Application.Current;
        if (app?.Dispatcher is not null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(RebuildMenu);
            return;
        }

        RebuildMenu();
    }

    private void RebuildMenu()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        var old = _notifyIcon.ContextMenuStrip;
        _notifyIcon.ContextMenuStrip = BuildMenu();
        old?.Dispose();
        _notifyIcon.Text = BuildTrayText();
    }

    private ContextMenuStrip BuildMenu()
    {
        var L = LocalizationService.Instance;
        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) => IsMenuOpen = true;
        menu.Closed += (_, _) => IsMenuOpen = false;
        menu.Items.Add(L.Get("Tray.Show"), null, (_, _) => ShowWindow());
        menu.Items.Add(L.Get("Tray.Hide"), null, (_, _) => HideWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L.Get("Tray.Settings"), null, (_, _) => OpenSettings());

        _clickThroughMenu = new ToolStripMenuItem(L.Get("Tray.ClickThrough"))
        {
            CheckOnClick = true,
            Checked = _settings.Config.ClickThrough,
        };
        _clickThroughMenu.Click += (_, _) => ToggleClickThrough();
        menu.Items.Add(_clickThroughMenu);

        _switchPetMenu = new ToolStripMenuItem(L.Get("Tray.SwitchPet"));
        RebuildSwitchPetMenu();
        menu.Items.Add(_switchPetMenu);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(L.Get("Tray.About"), null, (_, _) => ShowAbout());
        menu.Items.Add(L.Get("Tray.Exit"), null, (_, _) => ExitApp());
        return menu;
    }

    private void ToggleClickThrough()
    {
        if (_clickThroughMenu is null)
        {
            return;
        }

        _settings.Config.ClickThrough = _clickThroughMenu.Checked;
        _settings.Save();
    }

    private void RebuildSwitchPetMenu()
    {
        if (_switchPetMenu is null)
        {
            return;
        }

        _switchPetMenu.DropDownItems.Clear();
        var pets = PetCatalog.ListPets();
        if (pets.Count == 0)
        {
            _switchPetMenu.DropDownItems.Add(
                new ToolStripMenuItem(LocalizationService.Instance.Get("Tray.NoPets")) { Enabled = false });
            return;
        }

        foreach (var pet in pets)
        {
            var item = new ToolStripMenuItem(pet)
            {
                Checked = string.Equals(pet, _settings.Config.PetName, StringComparison.OrdinalIgnoreCase),
                Tag = pet,
            };
            item.Click += (_, _) => SwitchPet(pet);
            _switchPetMenu.DropDownItems.Add(item);
        }
    }

    private void SwitchPet(string petName)
    {
        _settings.SetPetName(petName);
        RebuildSwitchPetMenu();
        if (_notifyIcon is not null)
        {
            _notifyIcon.Text = BuildTrayText();
        }
    }

    private void OpenSettings()
    {
        // 托盘菜单在 WinForms 线程，交由设置窗自行切回 WPF UI 线程
        SettingsWindow.ShowOrActivate(_settings);
    }

    private void OnSettingsChanged()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Text = BuildTrayText();
        }

        if (_clickThroughMenu is not null)
        {
            _clickThroughMenu.Checked = _settings.Config.ClickThrough;
        }

        RebuildSwitchPetMenu();
        ApplyWindowSettings();
    }

    private void ApplyWindowSettings()
    {
        _window.Topmost = _settings.Config.Topmost;
    }

    private string BuildTrayText()
    {
        var name = _settings.Config.PetName;
        var text = $"DesktopPet - {name}";
        return text.Length <= 63 ? text : text[..63];
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

    private static void ShowAbout()
    {
        var L = LocalizationService.Instance;
        System.Windows.MessageBox.Show(
            L.Get("About.Body"),
            L.Get("About.Title"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
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
