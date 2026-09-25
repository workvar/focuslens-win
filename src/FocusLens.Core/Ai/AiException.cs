namespace FocusLens.Core.Ai;

public enum AiErrorKind
{
    MissingApiKey,
    Network,
    Unauthorized,
    RateLimited,
    ServerError,
    StreamInterrupted,
    Api,
    Parse,
    MissingOllamaModel,
    OllamaUnreachable,
    OllamaModelMissing,
}

/// <summary>A user-presentable AI failure.</summary>
public sealed class AiException : Exception
{
    public AiErrorKind Kind { get; }

    public AiException(AiErrorKind kind, string? detail = null, Exception? inner = null)
        : base(Describe(kind, detail), inner) => Kind = kind;

    private static string Describe(AiErrorKind kind, string? detail) => kind switch
    {
        AiErrorKind.MissingApiKey => "AI is disabled, add an API key in Settings.",
        AiErrorKind.Network => "Couldn't reach the AI service. Check your connection and try again.",
        AiErrorKind.Unauthorized => "Your API key was rejected. Re-enter it in Settings.",
        AiErrorKind.RateLimited => "Rate limited. Try again in a moment.",
        AiErrorKind.ServerError => $"The AI service is having trouble (HTTP {detail}). Try again.",
        AiErrorKind.StreamInterrupted => "Response was cut off.",
        AiErrorKind.Api => $"AI error: {detail}",
        AiErrorKind.Parse => "Failed to parse the AI response.",
        AiErrorKind.MissingOllamaModel => "Pick an Ollama model in Settings before chatting.",
        AiErrorKind.OllamaUnreachable => $"Couldn't reach Ollama at {detail}. Is `ollama serve` running?",
        AiErrorKind.OllamaModelMissing => $"Model `{detail}` isn't installed. Run `ollama pull {detail}` in a terminal.",
        _ => "AI error.",
    };
}
