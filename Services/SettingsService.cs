using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopPet.Core;

namespace DesktopPet.Services;

/// <summary>
/// Loads/saves user settings under %AppData%/DesktopPet/settings.json.
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _settingsPath;

    public PetConfig Config { get; private set; } = new();

    public event Action? Changed;

    public SettingsService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopPet");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
    }

    public void Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var loaded = JsonSerializer.Deserialize<PetConfig>(json, JsonOptions);
            if (loaded is not null)
            {
                Config = loaded;
                Config.WalkArea ??= new WalkAreaConfig();
            }
        }
        catch
        {
            // Keep defaults when settings file is corrupt.
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(Config, JsonOptions);
        File.WriteAllText(_settingsPath, json);
        Changed?.Invoke();
    }

    public void SetPetName(string petName)
    {
        if (string.Equals(Config.PetName, petName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Config.PetName = petName;
        Save();
    }
}
