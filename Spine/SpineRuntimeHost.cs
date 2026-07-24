using Spine;

namespace DesktopPet.Spine;

/// <summary>
/// Loads Spine atlas/skeleton assets and advances animation each frame.
/// </summary>
public sealed class SpineRuntimeHost : IDisposable
{
    private readonly WpfTextureLoader _textureLoader = new();
    private readonly AnimationController _animationController = new();
    private Atlas? _atlas;
    private AnimationState.TrackEntryDelegate? _completeHandler;

    public bool IsLoaded { get; private set; }

    public Skeleton? Skeleton { get; private set; }

    public AnimationState? AnimationState { get; private set; }

    public SkeletonData? SkeletonData { get; private set; }

    public AnimationController Animations => _animationController;

    public string? LoadedPetName { get; private set; }

    public event Action? AnimationCompleted;

    public void LoadPet(string petName)
    {
        DisposeRuntime();

        var export = PetAssetResolver.GetExportFolder(petName);
        var assets = PetAssetResolver.Resolve(export);

        _atlas = new Atlas(assets.AtlasPath, _textureLoader);
        SkeletonData = assets.SkeletonPath.EndsWith(".skel", StringComparison.OrdinalIgnoreCase)
            ? new SkeletonBinary(_atlas) { Scale = 1f }.ReadSkeletonData(assets.SkeletonPath)
            : new SkeletonJson(_atlas) { Scale = 1f }.ReadSkeletonData(assets.SkeletonPath);

        Skeleton = new Skeleton(SkeletonData);
        Skeleton.SetToSetupPoseCompat();
        Skeleton.UpdateWorldTransform(Physics.Update);

        var stateData = new AnimationStateData(SkeletonData);
        AnimationState = new AnimationState(stateData);
        _completeHandler = _ => AnimationCompleted?.Invoke();
        AnimationState.Complete += _completeHandler;

        _animationController.ResetCycle();
        _animationController.PlayIdle(AnimationState, SkeletonData, petName);

        LoadedPetName = petName;
        IsLoaded = true;
    }

    public void PlayIdle()
    {
        if (AnimationState is null || SkeletonData is null)
        {
            return;
        }

        _animationController.PlayIdle(AnimationState, SkeletonData, LoadedPetName);
    }

    public void PlayClick()
    {
        if (AnimationState is null || SkeletonData is null)
        {
            return;
        }

        _animationController.PlayClick(AnimationState, SkeletonData, LoadedPetName);
    }

    public void PlayDrag()
    {
        if (AnimationState is null || SkeletonData is null)
        {
            return;
        }

        _animationController.PlayDrag(AnimationState, SkeletonData, LoadedPetName);
    }

    public void PlayWalk(bool preferRun = false)
    {
        if (AnimationState is null || SkeletonData is null)
        {
            return;
        }

        _animationController.PlayWalk(AnimationState, SkeletonData, LoadedPetName, preferRun);
    }

    public void PlaySleep()
    {
        if (AnimationState is null || SkeletonData is null)
        {
            return;
        }

        _animationController.PlaySleep(AnimationState, SkeletonData, LoadedPetName);
    }

    public void PlayRandomAction()
    {
        if (AnimationState is null || SkeletonData is null)
        {
            return;
        }

        _animationController.PlayRandomAction(AnimationState, SkeletonData, LoadedPetName);
    }

    public void PlayNamed(string animationName, bool loop = false)
    {
        if (AnimationState is null || string.IsNullOrWhiteSpace(animationName))
        {
            return;
        }

        _animationController.Play(AnimationState, animationName, loop);
    }

    public IReadOnlyList<string> ListClickActions()
    {
        if (SkeletonData is null)
        {
            return [];
        }

        return PetAnimationMap.ListAvailable(SkeletonData, PetAction.Click, LoadedPetName);
    }

    public void SetFacing(bool faceRight)
    {
        if (Skeleton is null)
        {
            return;
        }

        var abs = Math.Abs(Skeleton.ScaleX);
        if (abs < 0.001f)
        {
            abs = 1f;
        }

        Skeleton.ScaleX = faceRight ? abs : -abs;
    }

    public void Update(float deltaSeconds)
    {
        if (!IsLoaded || Skeleton is null || AnimationState is null)
        {
            return;
        }

        AnimationState.Update(deltaSeconds);
        AnimationState.Apply(Skeleton);
        Skeleton.Update(deltaSeconds);
        Skeleton.UpdateWorldTransform(Physics.Update);
    }

    public void Dispose()
    {
        DisposeRuntime();
    }

    private void DisposeRuntime()
    {
        if (AnimationState is not null && _completeHandler is not null)
        {
            AnimationState.Complete -= _completeHandler;
        }

        _completeHandler = null;
        IsLoaded = false;
        LoadedPetName = null;
        AnimationState = null;
        Skeleton = null;
        SkeletonData = null;

        if (_atlas is not null)
        {
            _atlas.Dispose();
            _atlas = null;
        }
    }
}

internal static class SkeletonSetupExtensions
{
    public static void SetToSetupPoseCompat(this Skeleton skeleton)
    {
        skeleton.SetupPose();
    }
}
