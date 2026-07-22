namespace DesktopPet.Core;

/// <summary>Random pause ranges (seconds) for the autonomy loop.</summary>
public sealed class AutonomyConfig
{
    public SecondsRange PauseBeforeWalk { get; set; } = new() { Min = 12, Max = 28 };

    public SecondsRange PauseAfterWalk { get; set; } = new() { Min = 6, Max = 16 };

    public SecondsRange PauseAfterAct { get; set; } = new() { Min = 10, Max = 24 };

    public static AutonomyConfig CreateDefault() => new();

    /// <summary>
    /// Fills missing sections and clamps invalid ranges. Safe to call on loaded or null config.
    /// </summary>
    public static AutonomyConfig Normalize(AutonomyConfig? source)
    {
        var defaults = CreateDefault();
        if (source is null)
        {
            return defaults;
        }

        source.PauseBeforeWalk = NormalizeRange(source.PauseBeforeWalk, defaults.PauseBeforeWalk);
        source.PauseAfterWalk = NormalizeRange(source.PauseAfterWalk, defaults.PauseAfterWalk);
        source.PauseAfterAct = NormalizeRange(source.PauseAfterAct, defaults.PauseAfterAct);
        return source;
    }

    private static SecondsRange NormalizeRange(SecondsRange? range, SecondsRange fallback)
    {
        if (range is null)
        {
            return new SecondsRange { Min = fallback.Min, Max = fallback.Max };
        }

        var min = double.IsFinite(range.Min) ? Math.Max(0, range.Min) : fallback.Min;
        var max = double.IsFinite(range.Max) ? Math.Max(0, range.Max) : fallback.Max;
        if (max < min)
        {
            (min, max) = (max, min);
        }

        return new SecondsRange { Min = min, Max = max };
    }
}

public sealed class SecondsRange
{
    public double Min { get; set; }

    public double Max { get; set; }
}
