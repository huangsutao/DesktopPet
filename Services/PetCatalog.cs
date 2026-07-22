using System.IO;

namespace DesktopPet.Services;

/// <summary>
/// Discovers pet folders that contain an export directory with runtime assets.
/// </summary>
public static class PetCatalog
{
    public static IReadOnlyList<string> ListPets()
    {
        var petsRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Pets");
        if (!Directory.Exists(petsRoot))
        {
            return Array.Empty<string>();
        }

        return Directory.GetDirectories(petsRoot)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Where(name => HasExportAssets(Path.Combine(petsRoot, name!)))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();
    }

    private static bool HasExportAssets(string petFolder)
    {
        var export = Path.Combine(petFolder, "export");
        if (!Directory.Exists(export))
        {
            return false;
        }

        var hasAtlas = Directory.EnumerateFiles(export, "*.atlas").Any();
        var hasSkeleton =
            Directory.EnumerateFiles(export, "*.skel").Any() ||
            Directory.EnumerateFiles(export, "*.json").Any();
        return hasAtlas && hasSkeleton;
    }
}
