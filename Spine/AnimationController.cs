using Spine;

namespace DesktopPet.Spine;

/// <summary>
/// Maps pet states to Spine animation names and handles transitions.
/// </summary>
public sealed class AnimationController
{
    private readonly Random _rng = new();
    private int _clickIndex;

    private static readonly string[] LocomotionHints = ["walk", "run", "sneak"];

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

    /// <summary>
    /// Random one-shot from the click pool (excludes locomotion when possible).
    /// Does not advance the sequential click index.
    /// </summary>
    public void PlayRandomAction(AnimationState state, SkeletonData data, string? petFolderName = null)
    {
        var pool = PetAnimationMap.ListAvailable(data, PetAction.Click, petFolderName);
        if (pool.Count == 0)
        {
            return;
        }

        var nonLoco = pool.Where(n => !IsLocomotionName(n)).ToList();
        var pickFrom = nonLoco.Count > 0 ? nonLoco : pool;
        Play(state, pickFrom[_rng.Next(pickFrom.Count)], loop: false);
    }

    public void PlayWalk(
        AnimationState state,
        SkeletonData data,
        string? petFolderName = null,
        bool preferRun = false)
    {
        var pool = PetAnimationMap.ListAvailable(data, PetAction.Walk, petFolderName);
        if (pool.Count == 0)
        {
            // Fall back to any locomotion-like click candidate, then idle.
            pool = PetAnimationMap.ListAvailable(data, PetAction.Click, petFolderName)
                .Where(IsLocomotionName)
                .ToList();
        }

        if (pool.Count == 0)
        {
            PlayIdle(state, data, petFolderName);
            return;
        }

        string? name = null;
        if (preferRun)
        {
            name = pool.FirstOrDefault(n => n.Contains("run", StringComparison.OrdinalIgnoreCase));
        }

        name ??= pool.FirstOrDefault(n => n.Contains("walk", StringComparison.OrdinalIgnoreCase))
                 ?? pool[0];

        Play(state, name, loop: true);
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

    public void PlaySleep(AnimationState state, SkeletonData data, string? petFolderName = null)
    {
        var name = PetAnimationMap.Resolve(data, PetAction.Sleep, petFolderName);
        if (name is null)
        {
            PlayIdle(state, data, petFolderName);
            return;
        }

        // death 类动画播完停在末帧；其余循环作为睡姿
        var loop = !name.Contains("death", StringComparison.OrdinalIgnoreCase);
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

    private static bool IsLocomotionName(string name) =>
        LocomotionHints.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase));
}
