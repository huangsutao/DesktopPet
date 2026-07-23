using System.IO;
using System.Text.Json;
using System.Windows;

namespace DesktopPet.Services;

public sealed record LocaleInfo(string Code, string DisplayName, string FilePath);

/// <summary>
/// Loads UI strings from Locales/*.json. Add a new language by dropping another JSON file
/// (same keys as zh-CN.json); no code change required.
/// </summary>
public sealed class LocalizationService
{
    public const string DefaultLanguage = "zh-CN";
    public const string FallbackLanguage = "en";

    private readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal);
    private ResourceDictionary? _appDict;

    public static LocalizationService Instance { get; } = new();

    public string CurrentLanguage { get; private set; } = DefaultLanguage;

    public IReadOnlyList<LocaleInfo> AvailableLanguages { get; private set; } = [];

    public event Action? LanguageChanged;

    private LocalizationService()
    {
    }

    public void Initialize(string? preferredLanguage = null)
    {
        RefreshAvailableLanguages();
        ApplyLanguage(preferredLanguage ?? DefaultLanguage);
    }

    public void RefreshAvailableLanguages()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Locales");
        if (!Directory.Exists(dir))
        {
            AvailableLanguages = [];
            return;
        }

        var list = new List<LocaleInfo>();
        foreach (var path in Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly))
        {
            var code = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            var display = code;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("_displayName", out var nameEl))
                {
                    var name = nameEl.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        display = name!;
                    }
                }
            }
            catch
            {
                // Keep file-name as display name when JSON is invalid.
            }

            list.Add(new LocaleInfo(code, display, path));
        }

        AvailableLanguages = list
            .OrderBy(l => l.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void ApplyLanguage(string? languageCode)
    {
        RefreshAvailableLanguages();
        var code = ResolveLanguageCode(languageCode);
        var path = AvailableLanguages
            .FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase))
            ?.FilePath;

        _strings.Clear();
        if (path is not null && File.Exists(path))
        {
            LoadInto(_strings, path);
        }

        // Fill missing keys from fallback English, then Chinese.
        EnsureFallback(FallbackLanguage);
        if (!string.Equals(code, DefaultLanguage, StringComparison.OrdinalIgnoreCase))
        {
            EnsureFallback(DefaultLanguage);
        }

        CurrentLanguage = code;
        PushToApplicationResources();
        LanguageChanged?.Invoke();
    }

    public string Get(string key, string? fallback = null)
    {
        if (_strings.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
        {
            return value;
        }

        return fallback ?? key;
    }

    private string ResolveLanguageCode(string? preferred)
    {
        if (!string.IsNullOrWhiteSpace(preferred) &&
            AvailableLanguages.Any(l => string.Equals(l.Code, preferred, StringComparison.OrdinalIgnoreCase)))
        {
            return AvailableLanguages
                .First(l => string.Equals(l.Code, preferred, StringComparison.OrdinalIgnoreCase))
                .Code;
        }

        if (AvailableLanguages.Any(l => string.Equals(l.Code, DefaultLanguage, StringComparison.OrdinalIgnoreCase)))
        {
            return DefaultLanguage;
        }

        if (AvailableLanguages.Any(l => string.Equals(l.Code, FallbackLanguage, StringComparison.OrdinalIgnoreCase)))
        {
            return FallbackLanguage;
        }

        return AvailableLanguages.FirstOrDefault()?.Code ?? DefaultLanguage;
    }

    private void EnsureFallback(string code)
    {
        var path = AvailableLanguages
            .FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase))
            ?.FilePath;
        if (path is null || !File.Exists(path))
        {
            return;
        }

        var fallback = new Dictionary<string, string>(StringComparer.Ordinal);
        LoadInto(fallback, path);
        foreach (var (key, value) in fallback)
        {
            if (!_strings.ContainsKey(key))
            {
                _strings[key] = value;
            }
        }
    }

    private static void LoadInto(Dictionary<string, string> target, string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name.StartsWith('_'))
                {
                    continue;
                }

                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    var text = prop.Value.GetString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        target[prop.Name] = text;
                    }
                }
            }
        }
        catch
        {
            // Ignore corrupt locale file; caller may still have fallbacks.
        }
    }

    private void PushToApplicationResources()
    {
        var app = System.Windows.Application.Current;
        if (app is null)
        {
            return;
        }

        void Apply()
        {
            _appDict ??= new ResourceDictionary();
            if (!app.Resources.MergedDictionaries.Contains(_appDict))
            {
                app.Resources.MergedDictionaries.Add(_appDict);
            }

            _appDict.Clear();
            foreach (var (key, value) in _strings)
            {
                _appDict[key] = value;
            }
        }

        if (app.Dispatcher.CheckAccess())
        {
            Apply();
        }
        else
        {
            app.Dispatcher.Invoke(Apply);
        }
    }
}
