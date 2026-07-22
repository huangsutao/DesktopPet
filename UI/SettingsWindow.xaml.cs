using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using DesktopPet.Core;
using DesktopPet.Services;

namespace DesktopPet.UI;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settings;
    private static SettingsWindow? _instance;

    public SettingsWindow(SettingsService settings)
    {
        _settings = settings;
        InitializeComponent();
        LoadFromConfig();
    }

    public static void ShowOrActivate(SettingsService settings, Window? owner = null)
    {
        if (_instance is not null)
        {
            _instance.Activate();
            return;
        }

        _instance = new SettingsWindow(settings);
        if (owner is not null)
        {
            _instance.Owner = owner;
        }

        _instance.Closed += (_, _) => _instance = null;
        _instance.Show();
    }

    private void LoadFromConfig()
    {
        var cfg = _settings.Config;
        CurrentPetText.Text = cfg.PetName;
        ScaleSlider.Value = Math.Clamp(cfg.Scale, ScaleSlider.Minimum, ScaleSlider.Maximum);
        ScaleValueText.Text = ScaleSlider.Value.ToString("0.0", CultureInfo.InvariantCulture);
        TopmostCheck.IsChecked = cfg.Topmost;

        SelectWalkMode(cfg.WalkArea.Mode);
        MarginLeftBox.Text = cfg.WalkArea.MarginLeft.ToString(CultureInfo.InvariantCulture);
        MarginTopBox.Text = cfg.WalkArea.MarginTop.ToString(CultureInfo.InvariantCulture);
        MarginRightBox.Text = cfg.WalkArea.MarginRight.ToString(CultureInfo.InvariantCulture);
        MarginBottomBox.Text = cfg.WalkArea.MarginBottom.ToString(CultureInfo.InvariantCulture);
        UpdateMarginsVisibility();
    }

    private void SelectWalkMode(WalkAreaMode mode)
    {
        foreach (ComboBoxItem item in WalkModeCombo.Items)
        {
            if (string.Equals(item.Tag as string, mode.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                WalkModeCombo.SelectedItem = item;
                return;
            }
        }

        WalkModeCombo.SelectedIndex = 1;
    }

    private void ScaleSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ScaleValueText is null)
        {
            return;
        }

        ScaleValueText.Text = e.NewValue.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private void WalkModeCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateMarginsVisibility();
    }

    private void UpdateMarginsVisibility()
    {
        if (InsetMarginsPanel is null || WalkModeCombo.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        var tag = item.Tag as string;
        InsetMarginsPanel.Visibility =
            tag is "Inset" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var cfg = _settings.Config;
        cfg.Scale = ScaleSlider.Value;
        cfg.Topmost = TopmostCheck.IsChecked == true;

        if (WalkModeCombo.SelectedItem is ComboBoxItem modeItem &&
            Enum.TryParse<WalkAreaMode>(modeItem.Tag as string, ignoreCase: true, out var mode))
        {
            cfg.WalkArea.Mode = mode;
        }

        cfg.WalkArea.MarginLeft = ParseMargin(MarginLeftBox.Text, cfg.WalkArea.MarginLeft);
        cfg.WalkArea.MarginTop = ParseMargin(MarginTopBox.Text, cfg.WalkArea.MarginTop);
        cfg.WalkArea.MarginRight = ParseMargin(MarginRightBox.Text, cfg.WalkArea.MarginRight);
        cfg.WalkArea.MarginBottom = ParseMargin(MarginBottomBox.Text, cfg.WalkArea.MarginBottom);

        _settings.Save();
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private static double ParseMargin(string text, double fallback) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? Math.Max(0, value)
            : fallback;
}
