namespace DesktopPet.Spine;

/// <summary>Root document for pet-animations.json.</summary>
public sealed class PetAnimationConfigFile
{
    public bool IncludeAllNonIdleOnClick { get; set; } = true;

    /// <summary>Default speech-bubble lines (fallback for all pets).</summary>
    public List<string> BubbleLines { get; set; } = [];

    /// <summary>System role prompt used when calling the AI chat API for bubbles.</summary>
    public string AiRolePrompt { get; set; } = string.Empty;

    public PetActionCandidates Defaults { get; set; } = new();

    public List<PetAnimationProfile> Pets { get; set; } = [];
}

public sealed class PetActionCandidates
{
    public List<string> Idle { get; set; } = [];

    public List<string> Click { get; set; } = [];

    public List<string> Drag { get; set; } = [];

    public List<string> Walk { get; set; } = [];

    public List<string> Sleep { get; set; } = [];

    public IReadOnlyList<string> For(PetAction action) => action switch
    {
        PetAction.Idle => Idle,
        PetAction.Click => Click,
        PetAction.Drag => Drag,
        PetAction.Walk => Walk,
        PetAction.Sleep => Sleep,
        _ => Idle,
    };
}

/// <summary>
/// Per-pet overrides. <see cref="Match"/> is matched against skeleton name / pet folder (contains, ignore case).
/// </summary>
public sealed class PetAnimationProfile
{
    public string Match { get; set; } = string.Empty;

    public List<string>? Idle { get; set; }

    public List<string>? Click { get; set; }

    public List<string>? Drag { get; set; }

    public List<string>? Walk { get; set; }

    public List<string>? Sleep { get; set; }

    /// <summary>Optional per-pet bubble lines; falls back to root bubbleLines.</summary>
    public List<string>? BubbleLines { get; set; }

    public IEnumerable<string> For(PetAction action) => action switch
    {
        PetAction.Idle => Idle ?? [],
        PetAction.Click => Click ?? [],
        PetAction.Drag => Drag ?? [],
        PetAction.Walk => Walk ?? [],
        PetAction.Sleep => Sleep ?? [],
        _ => [],
    };
}
