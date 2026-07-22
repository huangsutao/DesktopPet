using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spine;

namespace DesktopPet.Spine;

/// <summary>
/// Resolves logical pet actions to Spine animation names via pet-animations.json.
/// </summary>
public static class PetAnimationMap
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static PetAnimationConfigFile? _config;
    private static string? _loadedPath;

    public static string ConfigFileName { get; } = "pet-animations.json";

    public static void Reload()
    {
        _config = null;
        _loadedPath = null;
        EnsureLoaded();
    }

    public static string? Resolve(SkeletonData data, PetAction action, string? petFolderName = null) =>
        ListAvailable(data, action, petFolderName).FirstOrDefault()
        ?? (data.Animations.Count > 0 ? data.Animations.Items[0].Name : null);

    /// <summary>
    /// All animations that can be used for the logical action (order preserved, duplicates removed).
    /// </summary>
    public static IReadOnlyList<string> ListAvailable(
        SkeletonData data,
        PetAction action,
        string? petFolderName = null)
    {
        var config = EnsureLoaded();
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void TryAdd(string name)
        {
            if (string.IsNullOrWhiteSpace(name) ||
                data.FindAnimation(name) is null ||
                !seen.Add(name))
            {
                return;
            }

            names.Add(name);
        }

        foreach (var name in PreferForPet(config, data.Name, petFolderName, action))
        {
            TryAdd(name);
        }

        foreach (var name in config.Defaults.For(action))
        {
            TryAdd(name);
        }

        if (action == PetAction.Click && config.IncludeAllNonIdleOnClick)
        {
            var idleName = ResolveIdleName(config, data, petFolderName);
            foreach (var anim in data.Animations)
            {
                if (idleName is not null &&
                    string.Equals(anim.Name, idleName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                TryAdd(anim.Name);
            }
        }

        return names;
    }

    private static string? ResolveIdleName(
        PetAnimationConfigFile config,
        SkeletonData data,
        string? petFolderName)
    {
        foreach (var name in PreferForPet(config, data.Name, petFolderName, PetAction.Idle)
                     .Concat(config.Defaults.Idle))
        {
            if (data.FindAnimation(name) is not null)
            {
                return name;
            }
        }

        return null;
    }

    private static IEnumerable<string> PreferForPet(
        PetAnimationConfigFile config,
        string? skeletonName,
        string? petFolderName,
        PetAction action)
    {
        foreach (var profile in config.Pets)
        {
            if (string.IsNullOrWhiteSpace(profile.Match))
            {
                continue;
            }

            if (Matches(skeletonName, profile.Match) || Matches(petFolderName, profile.Match))
            {
                return profile.For(action);
            }
        }

        return [];
    }

    private static bool Matches(string? haystack, string match) =>
        !string.IsNullOrEmpty(haystack) &&
        haystack.Contains(match, StringComparison.OrdinalIgnoreCase);

    private static PetAnimationConfigFile EnsureLoaded()
    {
        if (_config is not null)
        {
            return _config;
        }

        var path = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
        _loadedPath = path;

        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                _config = JsonSerializer.Deserialize<PetAnimationConfigFile>(json, JsonOptions)
                          ?? CreateBuiltInFallback();
                return _config;
            }
            catch
            {
                // Fall through to built-in defaults.
            }
        }

        _config = CreateBuiltInFallback();
        return _config;
    }

    /// <summary>Used only when pet-animations.json is missing or invalid.</summary>
    private static PetAnimationConfigFile CreateBuiltInFallback() => new()
    {
        IncludeAllNonIdleOnClick = true,
        Defaults = new PetActionCandidates
        {
            Idle = ["idle", "Idle", "stand", "breath"],
            Click =
            [
                "jump", "hit", "attack", "shoot", "portal", "aim",
                "hoverboard", "walk", "run", "sneak", "crouch",
                "morningstar pose", "idle-turn", "run-to-idle",
            ],
            Drag = ["fall", "jump", "hit", "idle", "run"],
            Walk = ["walk", "run", "sneak"],
        },
        Pets =
        [
            new PetAnimationProfile
            {
                Match = "alien",
                Idle = ["run"],
                Click = ["hit", "jump", "run", "death"],
                Walk = ["run"],
            },
            new PetAnimationProfile
            {
                Match = "hero",
                Click = ["attack", "jump", "crouch", "walk", "run", "morningstar pose"],
                Drag = ["fall", "jump"],
                Walk = ["walk", "run"],
            },
            new PetAnimationProfile
            {
                Match = "spineboy",
                Click = ["jump", "shoot", "portal", "aim", "hoverboard", "walk", "run"],
                Walk = ["walk", "run"],
            },
            new PetAnimationProfile
            {
                Match = "stretchyman",
                Click = ["sneak", "idle"],
                Walk = ["sneak"],
            },
        ],
    };
}

public enum PetAction
{
    Idle,
    Click,
    Drag,
    Walk,
}
