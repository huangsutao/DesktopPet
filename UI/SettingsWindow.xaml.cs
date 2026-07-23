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

    public static bool IsOpen => _instance is not null;

    public static void ShowOrActivate(SettingsService settings, Window? owner = null)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher is not null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.BeginInvoke(() => ShowOrActivate(settings, owner));
            return;
        }

        if (_instance is not null)
        {
            _instance.Activate();
            return;
        }

        _instance = new SettingsWindow(settings)
        {
            // 不挂 Owner：宠物窗每帧移动时，归属窗体会卡住设置窗输入
            Topmost = true,
        };

        _instance.Closed += (_, _) => _instance = null;
        _instance.Show();
        _instance.Activate();
    }

    private void LoadFromConfig()
    {
        var cfg = _settings.Config;
        CurrentPetText.Text = cfg.PetName;
        ScaleSlider.Value = Math.Clamp(cfg.Scale, ScaleSlider.Minimum, ScaleSlider.Maximum);
        ScaleValueText.Text = ScaleSlider.Value.ToString("0.00", CultureInfo.InvariantCulture);
        TopmostCheck.IsChecked = cfg.Topmost;
        ClickThroughCheck.IsChecked = cfg.ClickThrough;

        SelectWalkMode(cfg.WalkArea.Mode);
        MarginLeftBox.Text = cfg.WalkArea.MarginLeft.ToString(CultureInfo.InvariantCulture);
        MarginTopBox.Text = cfg.WalkArea.MarginTop.ToString(CultureInfo.InvariantCulture);
        MarginRightBox.Text = cfg.WalkArea.MarginRight.ToString(CultureInfo.InvariantCulture);
        MarginBottomBox.Text = cfg.WalkArea.MarginBottom.ToString(CultureInfo.InvariantCulture);
        UpdateMarginsVisibility();

        var autonomy = AutonomyConfig.Normalize(cfg.Autonomy);
        FillRange(PauseBeforeWalkMinBox, PauseBeforeWalkMaxBox, autonomy.PauseBeforeWalk);
        FillRange(PauseAfterWalkMinBox, PauseAfterWalkMaxBox, autonomy.PauseAfterWalk);
        FillRange(PauseAfterActMinBox, PauseAfterActMaxBox, autonomy.PauseAfterAct);

        var sleep = SleepConfig.Normalize(cfg.Sleep);
        SleepEnabledCheck.IsChecked = sleep.Enabled;
        SleepIdleSecondsBox.Text = FormatSeconds(sleep.IdleSeconds);
        UpdateSleepIdleVisibility();

        var bubble = BubbleConfig.Normalize(cfg.Bubble);
        BubbleEnabledCheck.IsChecked = bubble.Enabled;

        var ai = AiConfig.Normalize(cfg.Ai);
        AiEnabledCheck.IsChecked = ai.Enabled;
        AiUrlBox.Text = ai.Url;
        AiApiKeyBox.Password = ai.ApiKey;
        AiModelIdBox.Text = ai.ModelId;
        AiCountryBox.Text = ai.Country;
        AiCityBox.Text = ai.City;
        UpdateAiParamsVisibility();
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

        ScaleValueText.Text = e.NewValue.ToString("0.00", CultureInfo.InvariantCulture);
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

    private void SleepEnabledCheck_OnChanged(object sender, RoutedEventArgs e) =>
        UpdateSleepIdleVisibility();

    private void UpdateSleepIdleVisibility()
    {
        if (SleepIdlePanel is null)
        {
            return;
        }

        SleepIdlePanel.Visibility =
            SleepEnabledCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AiEnabledCheck_OnChanged(object sender, RoutedEventArgs e) =>
        UpdateAiParamsVisibility();

    private void UpdateAiParamsVisibility()
    {
        if (AiParamsPanel is null)
        {
            return;
        }

        AiParamsPanel.Visibility =
            AiEnabledCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var cfg = _settings.Config;
        cfg.Scale = ScaleSlider.Value;
        cfg.Topmost = TopmostCheck.IsChecked == true;
        cfg.ClickThrough = ClickThroughCheck.IsChecked == true;

        if (WalkModeCombo.SelectedItem is ComboBoxItem modeItem &&
            Enum.TryParse<WalkAreaMode>(modeItem.Tag as string, ignoreCase: true, out var mode))
        {
            cfg.WalkArea.Mode = mode;
        }

        cfg.WalkArea.MarginLeft = ParseNonNegative(MarginLeftBox.Text, cfg.WalkArea.MarginLeft);
        cfg.WalkArea.MarginTop = ParseNonNegative(MarginTopBox.Text, cfg.WalkArea.MarginTop);
        cfg.WalkArea.MarginRight = ParseNonNegative(MarginRightBox.Text, cfg.WalkArea.MarginRight);
        cfg.WalkArea.MarginBottom = ParseNonNegative(MarginBottomBox.Text, cfg.WalkArea.MarginBottom);

        var defaults = AutonomyConfig.CreateDefault();
        cfg.Autonomy = AutonomyConfig.Normalize(new AutonomyConfig
        {
            PauseBeforeWalk = ReadRange(
                PauseBeforeWalkMinBox,
                PauseBeforeWalkMaxBox,
                cfg.Autonomy?.PauseBeforeWalk ?? defaults.PauseBeforeWalk),
            PauseAfterWalk = ReadRange(
                PauseAfterWalkMinBox,
                PauseAfterWalkMaxBox,
                cfg.Autonomy?.PauseAfterWalk ?? defaults.PauseAfterWalk),
            PauseAfterAct = ReadRange(
                PauseAfterActMinBox,
                PauseAfterActMaxBox,
                cfg.Autonomy?.PauseAfterAct ?? defaults.PauseAfterAct),
        });

        var sleepDefaults = SleepConfig.CreateDefault();
        cfg.Sleep = SleepConfig.Normalize(new SleepConfig
        {
            Enabled = SleepEnabledCheck.IsChecked == true,
            IdleSeconds = ParseNonNegative(
                SleepIdleSecondsBox.Text,
                cfg.Sleep?.IdleSeconds ?? sleepDefaults.IdleSeconds),
        });

        cfg.Bubble = BubbleConfig.Normalize(new BubbleConfig
        {
            Enabled = BubbleEnabledCheck.IsChecked == true,
        });

        cfg.Ai = AiConfig.Normalize(new AiConfig
        {
            Enabled = AiEnabledCheck.IsChecked == true,
            Url = AiUrlBox.Text,
            ApiKey = AiApiKeyBox.Password,
            ModelId = AiModelIdBox.Text,
            Country = AiCountryBox.Text,
            City = AiCityBox.Text,
        });

        _settings.Save();
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private static void FillRange(
        System.Windows.Controls.TextBox minBox,
        System.Windows.Controls.TextBox maxBox,
        SecondsRange range)
    {
        minBox.Text = FormatSeconds(range.Min);
        maxBox.Text = FormatSeconds(range.Max);
    }

    private static SecondsRange ReadRange(
        System.Windows.Controls.TextBox minBox,
        System.Windows.Controls.TextBox maxBox,
        SecondsRange fallback) =>
        new()
        {
            Min = ParseNonNegative(minBox.Text, fallback.Min),
            Max = ParseNonNegative(maxBox.Text, fallback.Max),
        };

    private static string FormatSeconds(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private static double ParseNonNegative(string text, double fallback) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? Math.Max(0, value)
            : fallback;
}
