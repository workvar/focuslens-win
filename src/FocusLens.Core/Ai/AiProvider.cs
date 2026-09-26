namespace FocusLens.Core.Ai;

/// <summary>Which model backend answers chat and summaries.</summary>
public abstract record AiProvider
{
    public sealed record Claude(string ApiKey) : AiProvider;
    public sealed record OpenAi(string ApiKey) : AiProvider;
    public sealed record Nvidia(string ApiKey) : AiProvider;
    public sealed record DeepSeek(string ApiKey) : AiProvider;
    public sealed record Ollama(string Host, string Model) : AiProvider;

    /// <summary>True when prompts leave this PC.</summary>
    public bool IsCloud => this is Claude or OpenAi or Nvidia or DeepSeek;
}
