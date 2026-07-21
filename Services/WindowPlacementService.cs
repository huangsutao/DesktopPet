using System.Windows;

namespace DesktopPet.Services;

/// <summary>
/// Saves and restores pet window position; clamps to working area.
/// </summary>
public sealed class WindowPlacementService
{
    public void ClampToScreen(Window window)
    {
        // TODO: keep window inside virtual screen bounds
        _ = window;
    }

    public void Save(Window window)
    {
        // TODO: persist Left/Top
        _ = window;
    }

    public void Restore(Window window)
    {
        // TODO: restore Left/Top from settings
        _ = window;
    }
}
