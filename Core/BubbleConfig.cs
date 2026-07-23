namespace DesktopPet.Core;

/// <summary>Speech bubble dialogue above the pet.</summary>
public sealed class BubbleConfig
{
    public bool Enabled { get; set; } = true;

    public static BubbleConfig CreateDefault() => new();

    public static BubbleConfig Normalize(BubbleConfig? source) =>
        source ?? CreateDefault();
}
