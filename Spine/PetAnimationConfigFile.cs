namespace DesktopPet.Spine;

/// <summary>Root document for pet-animations.json.</summary>
public sealed class PetAnimationConfigFile
{
    public bool IncludeAllNonIdleOnClick { get; set; } = true;

    public PetActionCandidates Defaults { get; set; } = new();

    public List<PetAnimationProfile> Pets { get; set; } = [];
}

public sealed class PetActionCandidates
{
    public List<string> Idle { get; set; } = [];

    public List<string> Click { get; set; } = [];

    public List<string> Drag { get; set; } = [];

    public IReadOnlyList<string> For(PetAction action) => action switch
    {
        PetAction.Idle => Idle,
        PetAction.Click => Click,
        PetAction.Drag => Drag,
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

    public IEnumerable<string> For(PetAction action) => action switch
    {
        PetAction.Idle => Idle ?? [],
        PetAction.Click => Click ?? [],
        PetAction.Drag => Drag ?? [],
        _ => [],
    };
}
