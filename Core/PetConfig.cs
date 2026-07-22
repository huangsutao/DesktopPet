namespace DesktopPet.Core;

public sealed class PetConfig
{
    public string PetName { get; set; } = "default";

    public double Scale { get; set; } = 1.0;

    public bool Topmost { get; set; } = true;

    public bool ClickThrough { get; set; }

    public WalkAreaConfig WalkArea { get; set; } = new();
}
