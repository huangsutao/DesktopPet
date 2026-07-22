using Spine;

namespace DesktopPet.Spine;

/// <summary>
/// Maps pet states to Spine animation names and handles transitions.
/// </summary>
public sealed class AnimationController
{
    public string CurrentAnimation { get; private set; } = string.Empty;

    public void PlayIdle(AnimationState state, SkeletonData data) =>
        PlayAction(state, data, PetAction.Idle, loop: true);

    public void PlayClick(AnimationState state, SkeletonData data) =>
        PlayAction(state, data, PetAction.Click, loop: false);

    public void PlayDrag(AnimationState state, SkeletonData data) =>
        PlayAction(state, data, PetAction.Drag, loop: true);

    public void PlayAction(AnimationState state, SkeletonData data, PetAction action, bool loop)
    {
        var name = PetAnimationMap.Resolve(data, action);
        if (name is null)
        {
            return;
        }

        Play(state, name, loop);
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
