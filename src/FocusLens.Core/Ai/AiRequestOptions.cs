namespace FocusLens.Core.Ai;

/// <summary>
/// Per-request knobs for <see cref="StreamingAiClient"/>. Chat and meeting summaries use
/// <see cref="Standard"/>, which keeps each provider's defaults. Short yes/no judgements
/// (Focus Mode) use <see cref="Classification"/>, so a local model answers in a few tokens
/// instead of writing, or silently "thinking", a whole paragraph.
/// </summary>
public sealed record AiRequestOptions
{
    /// <summary>Upper bound on generated tokens. Null keeps the provider default.</summary>
    public int? MaxTokens { get; init; }
    public double? Temperature { get; init; }

    /// <summary>Asks reasoning models to answer directly (Ollama <c>think: false</c>).</summary>
    public bool DisableThinking { get; init; }

    /// <summary>How long Ollama keeps the model loaded after the request ("10m"). Null keeps the default.</summary>
    public string? KeepAlive { get; init; }

    /// <summary>Ollama only: a different model for this request. Null uses the model from the AI tab.</summary>
    public string? OllamaModel { get; init; }

    public static AiRequestOptions Standard { get; } = new();

    /// <summary>One word back (ON or OFF), no reasoning, and the model stays warm between checks.</summary>
    public static AiRequestOptions Classification { get; } = new()
    {
        MaxTokens = 8,
        Temperature = 0,
        DisableThinking = true,
        KeepAlive = "10m",
    };
}
