using System.IO;

namespace DesktopPet.Spine;

/// <summary>
/// Picks non-PMA atlas + preferred skeleton file from a pet export folder.
/// </summary>
public static class PetAssetResolver
{
    public sealed record ResolvedAssets(string AtlasPath, string SkeletonPath);

    public static ResolvedAssets Resolve(string exportFolder)
    {
        if (!Directory.Exists(exportFolder))
        {
            throw new DirectoryNotFoundException($"Export folder not found: {exportFolder}");
        }

        var atlas = PickAtlas(exportFolder)
            ?? throw new FileNotFoundException($"No atlas found in {exportFolder}");
        var skeleton = PickSkeleton(exportFolder)
            ?? throw new FileNotFoundException($"No skeleton found in {exportFolder}");
        return new ResolvedAssets(atlas, skeleton);
    }

    public static string GetExportFolder(string petName) =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "Pets", petName, "export");

    private static string? PickAtlas(string exportFolder)
    {
        var atlases = Directory.GetFiles(exportFolder, "*.atlas");
        // Prefer non-PMA, non-special atlases (e.g. skip spineboy-run).
        return atlases
            .OrderBy(path =>
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var score = 0;
                if (name.EndsWith("-pma", StringComparison.OrdinalIgnoreCase)) score += 100;
                if (name.Contains("-run", StringComparison.OrdinalIgnoreCase)) score += 50;
                return score;
            })
            .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static string? PickSkeleton(string exportFolder)
    {
        var skels = Directory.GetFiles(exportFolder, "*.skel");
        var jsons = Directory.GetFiles(exportFolder, "*.json");

        static int Score(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (name.EndsWith("-pro", StringComparison.OrdinalIgnoreCase)) return 0;
            if (name.EndsWith("-ess", StringComparison.OrdinalIgnoreCase)) return 10;
            return 5;
        }

        var bestSkel = skels.OrderBy(Score).ThenBy(p => p, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        if (bestSkel is not null)
        {
            return bestSkel;
        }

        return jsons.OrderBy(Score).ThenBy(p => p, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
    }
}
