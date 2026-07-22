namespace DesktopPet.Core;

public sealed class PetConfig
{
    /// <summary>Bump when applying one-time settings migrations.</summary>
    public int ConfigVersion { get; set; } = 3;

    public string PetName { get; set; } = "default";

    public double Scale { get; set; } = 0.25;

    public bool Topmost { get; set; } = true;

    public bool ClickThrough { get; set; }

    public WalkAreaConfig WalkArea { get; set; } = new();
}
