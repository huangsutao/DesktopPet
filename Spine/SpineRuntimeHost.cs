namespace DesktopPet.Spine;

/// <summary>
/// Loads Spine atlas/skeleton assets and advances animation each frame.
/// </summary>
public sealed class SpineRuntimeHost : IDisposable
{
    public bool IsLoaded { get; private set; }

    public void Load(string petFolder)
    {
        // TODO: load atlas + skeleton from Assets/Pets/{name}
        IsLoaded = false;
        _ = petFolder;
    }

    public void Update(float deltaSeconds)
    {
        if (!IsLoaded)
        {
            return;
        }

        // TODO: skeleton.Update(deltaSeconds)
        _ = deltaSeconds;
    }

    public void Dispose()
    {
        // TODO: dispose Spine resources
    }
}
