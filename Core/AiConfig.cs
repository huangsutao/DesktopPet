namespace DesktopPet.Core;

/// <summary>OpenAI-compatible chat API settings for speech bubbles.</summary>
public sealed class AiConfig
{
    public bool Enabled { get; set; }

    /// <summary>Base URL (e.g. https://api.openai.com/v1) or full .../chat/completions endpoint.</summary>
    public string Url { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ModelId { get; set; } = string.Empty;

    public static AiConfig CreateDefault() => new();

    public static AiConfig Normalize(AiConfig? source)
    {
        if (source is null)
        {
            return CreateDefault();
        }

        source.Url = (source.Url ?? string.Empty).Trim();
        source.ApiKey = (source.ApiKey ?? string.Empty).Trim();
        source.ModelId = (source.ModelId ?? string.Empty).Trim();
        return source;
    }

    public bool IsReady =>
        Enabled &&
        !string.IsNullOrWhiteSpace(Url) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(ModelId);
}
