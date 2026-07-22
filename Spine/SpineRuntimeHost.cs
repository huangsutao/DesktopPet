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

    public bool IsLoaded { get; private set; }

    public Skeleton? Skeleton { get; private set; }

    public AnimationState? AnimationState { get; private set; }

    public SkeletonData? SkeletonData { get; private set; }

    public AnimationController Animations => _animationController;

    public string? LoadedPetName { get; private set; }

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
        _animationController.PlayIdle(AnimationState, SkeletonData);

        LoadedPetName = petName;
        IsLoaded = true;
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
