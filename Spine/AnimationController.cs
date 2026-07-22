using Spine;

namespace DesktopPet.Spine;

/// <summary>
/// Maps pet states to Spine animation names and handles transitions.
/// </summary>
public sealed class AnimationController
{
    private int _clickIndex;

    public string CurrentAnimation { get; private set; } = string.Empty;

    public void ResetCycle() => _clickIndex = 0;

    public void PlayIdle(AnimationState state, SkeletonData data, string? petFolderName = null)
    {
        var name = PetAnimationMap.Resolve(data, PetAction.Idle, petFolderName);
        if (name is null)
        {
            return;
        }

        Play(state, name, loop: true);
    }

    public void PlayClick(AnimationState state, SkeletonData data, string? petFolderName = null)
    {
        var pool = PetAnimationMap.ListAvailable(data, PetAction.Click, petFolderName);
        if (pool.Count == 0)
        {
            return;
        }

        var name = pool[_clickIndex % pool.Count];
        _clickIndex = (_clickIndex + 1) % pool.Count;
        Play(state, name, loop: false);
    }

    public void PlayDrag(AnimationState state, SkeletonData data, string? petFolderName = null)
    {
        var name = PetAnimationMap.Resolve(data, PetAction.Drag, petFolderName);
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
}
