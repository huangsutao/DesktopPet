using Spine;

namespace DesktopPet.Spine;

/// <summary>
/// Maps pet states to Spine animation names and handles fallbacks.
/// </summary>
public sealed class AnimationController
{
    private static readonly string[] IdleCandidates = ["idle", "Idle", "stand", "breath"];
    private static readonly string[] FallbackCandidates = ["walk", "run", "sneak", "hoverboard"];

    public string CurrentAnimation { get; private set; } = string.Empty;

    public void PlayIdle(AnimationState state, SkeletonData data)
    {
        var name = ResolveIdle(data);
        if (name is null)
        {
            return;
        }

        Play(state, name, loop: true);
    }

    public void Play(AnimationState state, string animationName, bool loop = true)
    {
        if (state.Data.SkeletonData.FindAnimation(animationName) is null)
        {
            return;
        }

        CurrentAnimation = animationName;
        state.SetAnimation(0, animationName, loop);
    }

    public static string? ResolveIdle(SkeletonData data)
    {
        foreach (var name in IdleCandidates)
        {
            if (data.FindAnimation(name) is not null)
            {
                return name;
            }
        }

        foreach (var name in FallbackCandidates)
        {
            if (data.FindAnimation(name) is not null)
            {
                return name;
            }
        }

        return data.Animations.Count > 0 ? data.Animations.Items[0].Name : null;
    }
}
