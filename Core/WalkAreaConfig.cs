namespace DesktopPet.Core;

public enum WalkAreaMode
{
    WorkArea,
    Inset,
    CustomRect,
}

public sealed class WalkAreaConfig
{
    public WalkAreaMode Mode { get; set; } = WalkAreaMode.Inset;

    public double MarginLeft { get; set; } = 40;

    public double MarginTop { get; set; } = 40;

    public double MarginRight { get; set; } = 40;

    public double MarginBottom { get; set; } = 80;

    /// <summary>Used when Mode is CustomRect; coordinates are relative to WorkArea.</summary>
    public double CustomX { get; set; }

    public double CustomY { get; set; }

    public double CustomWidth { get; set; } = 800;

    public double CustomHeight { get; set; } = 600;
}
