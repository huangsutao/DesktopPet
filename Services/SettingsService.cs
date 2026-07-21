using DesktopPet.Core;

namespace DesktopPet.Services;

/// <summary>
/// Loads/saves user settings under %AppData%/DesktopPet/settings.json.
/// </summary>
public sealed class SettingsService
{
    public PetConfig Config { get; private set; } = new();

    public void Load()
    {
        // TODO: read JSON from AppData
    }

    public void Save()
    {
        // TODO: write JSON to AppData
    }
}
