namespace DesktopPet.Core;

public sealed class PetConfig
{
    /// <summary>Bump when applying one-time settings migrations.</summary>
    public int ConfigVersion { get; set; } = 3;

    public string PetName { get; set; } = "default";

    public double Scale { get; set; } = 0.25;

    public bool Topmost { get; set; } = true;

    public bool ClickThrough { get; set; }

    /// <summary>UI language code matching Locales/{code}.json (e.g. zh-CN, en).</summary>
    public string UiLanguage { get; set; } = "zh-CN";

    public WalkAreaConfig WalkArea { get; set; } = new();

    public AutonomyConfig Autonomy { get; set; } = new();

    public SleepConfig Sleep { get; set; } = new();

    public BubbleConfig Bubble { get; set; } = new();

    public AiConfig Ai { get; set; } = new();
}
