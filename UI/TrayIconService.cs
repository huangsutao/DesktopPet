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

    public TrayIconService(Window window, SettingsService settings)
    {
        _window = window;
        _settings = settings;
        _settings.Changed += OnSettingsChanged;
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
        menu.Items.Add("设置", null, (_, _) => OpenSettings());

        _switchPetMenu = new ToolStripMenuItem("切换形象");
        RebuildSwitchPetMenu();
        menu.Items.Add(_switchPetMenu);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("关于", null, (_, _) => ShowAbout());
        menu.Items.Add("退出", null, (_, _) => ExitApp());
        return menu;
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
            _switchPetMenu.DropDownItems.Add(new ToolStripMenuItem("（未找到可用形象）") { Enabled = false });
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
        SettingsWindow.ShowOrActivate(_settings, _window.IsVisible ? _window : null);
    }

    private void OnSettingsChanged()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Text = BuildTrayText();
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
        System.Windows.MessageBox.Show(
            "DesktopPet\n基于 WPF + Spine 的桌面宠物。",
            "关于",
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
