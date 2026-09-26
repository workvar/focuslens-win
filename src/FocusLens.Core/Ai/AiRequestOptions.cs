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

    /// <summary>
    /// Asks reasoning models to answer directly. Ollama gets <c>think: false</c>. Anthropic gets
    /// <c>thinking: {type: disabled}</c>. OpenAI reasoning models (gpt-5, o-series) get
    /// <c>reasoning_effort: none</c>; gpt-4o does not, because that parameter is rejected there.
    /// </summary>
    public bool DisableThinking { get; init; }

    /// <summary>How long Ollama keeps the model loaded after the request ("10m"). Null keeps the default.</summary>
    public string? KeepAlive { get; init; }

    /// <summary>Claude or OpenAI model id for this request. Null keeps that provider's default.</summary>
    public string? Model { get; init; }

    /// <summary>Ollama only: a different model for this request. Null uses the model from the AI tab.</summary>
    public string? OllamaModel { get; init; }

    /// <summary>
    /// A PNG sent with the prompt, for the models that can look at one. Guide attaches the screen
    /// it is planning against. Never set for a model that cannot see: most providers answer 400
    /// rather than ignoring it.
    /// </summary>
    public byte[]? ImagePng { get; init; }

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
