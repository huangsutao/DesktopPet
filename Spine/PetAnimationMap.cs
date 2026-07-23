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
    /// Speech bubble lines: Locales/_bubbleLines → pet override → root pet-animations → built-in.
    /// </summary>
    public static IReadOnlyList<string> GetBubbleLines(string? petFolderName = null)
    {
        var localized = DesktopPet.Services.LocalizationService.Instance.GetBubbleLines();
        if (localized.Count > 0)
        {
            return localized;
        }

        var config = EnsureLoaded();
        foreach (var profile in config.Pets)
        {
            if (string.IsNullOrWhiteSpace(profile.Match) ||
                string.IsNullOrEmpty(petFolderName) ||
                !petFolderName.Contains(profile.Match, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (profile.BubbleLines is { Count: > 0 })
            {
                return SanitizeLines(profile.BubbleLines);
            }

            break;
        }

        if (config.BubbleLines is { Count: > 0 })
        {
            return SanitizeLines(config.BubbleLines);
        }

        return BuiltInBubbleLines;
    }

    /// <summary>
    /// AI system role: Locales Ai.RolePrompt → pet-animations.json → built-in.
    /// </summary>
    public static string GetAiRolePrompt()
    {
        var fromLocale = DesktopPet.Services.LocalizationService.Instance.Get("Ai.RolePrompt");
        if (!string.IsNullOrWhiteSpace(fromLocale) &&
            !string.Equals(fromLocale, "Ai.RolePrompt", StringComparison.Ordinal))
        {
            return fromLocale.Trim();
        }

        var prompt = EnsureLoaded().AiRolePrompt?.Trim();
        return string.IsNullOrWhiteSpace(prompt) ? BuiltInAiRolePrompt : prompt;
    }

    private static IReadOnlyList<string> SanitizeLines(IEnumerable<string> lines) =>
        lines.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()).ToList();

    private static readonly string[] BuiltInBubbleLines =
    [
        "今天也要加油呀～",
        "摸鱼一时爽，一直摸鱼一直爽。",
        "我在这儿陪着你呢。",
    ];

    private const string BuiltInAiRolePrompt =
        "你是一只可爱的桌面宠物，陪在用户桌面旁。说话简短、温柔、俏皮，每次只说一两句中文，不超过40个字，不要用引号包裹，不要解释自己是AI。" +
        "用户消息会提供当前时间、日期、地点与天气等情境，请据此自然回应；可轻提应季感受，不要编造具体新闻。";

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
        BubbleLines = BuiltInBubbleLines.ToList(),
        AiRolePrompt = BuiltInAiRolePrompt,
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
            Sleep = ["sleep", "Sleep", "death", "crouch", "idle"],
        },
        Pets =
        [
            new PetAnimationProfile
            {
                Match = "alien",
                Idle = ["run"],
                Click = ["hit", "jump", "run", "death"],
                Walk = ["run"],
                Sleep = ["death"],
            },
            new PetAnimationProfile
            {
                Match = "hero",
                Click = ["attack", "jump", "crouch", "walk", "run", "morningstar pose"],
                Drag = ["fall", "jump"],
                Walk = ["walk", "run"],
                Sleep = ["crouch", "idle"],
            },
            new PetAnimationProfile
            {
                Match = "spineboy",
                Click = ["jump", "shoot", "portal", "aim", "hoverboard", "walk", "run"],
                Walk = ["walk", "run"],
                Sleep = ["death", "idle"],
            },
            new PetAnimationProfile
            {
                Match = "stretchyman",
                Click = ["sneak", "idle"],
                Walk = ["sneak"],
                Sleep = ["idle"],
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
    Sleep,
}
