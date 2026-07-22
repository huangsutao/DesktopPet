using Spine;

namespace DesktopPet.Spine;

/// <summary>
/// Resolves logical pet actions to concrete Spine animation names for each pet.
/// </summary>
public static class PetAnimationMap
{
    private static readonly string[] IdleCandidates =
        ["idle", "Idle", "stand", "breath", "walk", "run", "sneak", "hoverboard"];

    private static readonly string[] ClickCandidates =
        ["jump", "hit", "attack", "shoot", "portal", "morningstar pose"];

    private static readonly string[] DragCandidates =
        ["fall", "jump", "hit", "idle", "run"];

    public static string? Resolve(SkeletonData data, PetAction action)
    {
        var preferred = PreferForPet(data.Name, action);
        foreach (var name in preferred.Concat(Candidates(action)))
        {
            if (data.FindAnimation(name) is not null)
            {
                return name;
            }
        }

        return data.Animations.Count > 0 ? data.Animations.Items[0].Name : null;
    }

    private static IEnumerable<string> PreferForPet(string? skeletonName, PetAction action)
    {
        var key = (skeletonName ?? string.Empty).ToLowerInvariant();
        return (key, action) switch
        {
            (var n, PetAction.Idle) when n.Contains("alien") => ["run", "jump"],
            (var n, PetAction.Click) when n.Contains("alien") => ["hit", "jump"],
            (var n, PetAction.Click) when n.Contains("hero") => ["attack", "jump"],
            (var n, PetAction.Click) when n.Contains("spineboy") || n.Contains("default") => ["jump", "shoot"],
            (var n, PetAction.Click) when n.Contains("stretchyman") => ["sneak", "idle"],
            (var n, PetAction.Drag) when n.Contains("hero") => ["fall", "jump"],
            _ => Array.Empty<string>(),
        };
    }

    private static string[] Candidates(PetAction action) => action switch
    {
        PetAction.Idle => IdleCandidates,
        PetAction.Click => ClickCandidates,
        PetAction.Drag => DragCandidates,
        _ => IdleCandidates,
    };
}

public enum PetAction
{
    Idle,
    Click,
    Drag,
}
