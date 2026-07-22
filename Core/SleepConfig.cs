namespace DesktopPet.Core;

/// <summary>Sleep after a period without click/drag interaction.</summary>
public sealed class SleepConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>Seconds without click/drag before entering sleep.</summary>
    public double IdleSeconds { get; set; } = 300;

    public static SleepConfig CreateDefault() => new();

    public static SleepConfig Normalize(SleepConfig? source)
    {
        var defaults = CreateDefault();
        if (source is null)
        {
            return defaults;
        }

        source.IdleSeconds = double.IsFinite(source.IdleSeconds)
            ? Math.Clamp(source.IdleSeconds, 10, 24 * 60 * 60)
            : defaults.IdleSeconds;
        return source;
    }
}
