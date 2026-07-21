namespace DesktopPet.Spine;

/// <summary>
/// Maps pet states to Spine animation names and handles transitions.
/// </summary>
public sealed class AnimationController
{
    public string CurrentAnimation { get; private set; } = "idle";

    public void Play(string animationName, bool loop = true)
    {
        CurrentAnimation = animationName;
        // TODO: set AnimationState track
        _ = loop;
    }
}
