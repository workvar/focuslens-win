using FocusLens.Core.Ai.Streams;
using FocusLens.Core.Models;

namespace FocusLens.Core.Ai;

/// <summary>
/// Streams a model response as text deltas. The provider is resolved on every call
/// so a Settings change takes effect immediately.
/// </summary>
public sealed class StreamingAiClient
{
    private readonly Func<AiProvider> _providerSource;
    private readonly HttpClient _http;

    public StreamingAiClient(Func<AiProvider> providerSource, HttpClient? http = null)
    {
        _providerSource = providerSource;
        _http = http ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    }

    public AiProvider Provider => _providerSource();

    public IAsyncEnumerable<string> StreamAsync(
        string prompt, IReadOnlyList<Message> history, CancellationToken ct = default) =>
        StreamAsync(prompt, history, AiRequestOptions.Standard, ct);

    /// <summary>Returns token deltas. <paramref name="options"/> limits length and reasoning for short answers.</summary>
    public IAsyncEnumerable<string> StreamAsync(
        string prompt, IReadOnlyList<Message> history, AiRequestOptions options, CancellationToken ct = default)
    {
        IChatStream stream = Provider switch
        {
            AiProvider.Claude c => new ClaudeStream(c.ApiKey),
            AiProvider.OpenAi o => new OpenAiStream(o.ApiKey),
            AiProvider.Nvidia n => new OpenAiStream(
                n.ApiKey, "https://integrate.api.nvidia.com/v1/chat/completions",
                "meta/llama-3.3-70b-instruct", ChatCompletionsKind.Nvidia),
            AiProvider.DeepSeek d => new OpenAiStream(
                d.ApiKey, "https://api.deepseek.com/chat/completions",
                "deepseek-flash", ChatCompletionsKind.DeepSeek),
            AiProvider.Ollama l => new OllamaStream(l.Host, l.Model),
            _ => throw new AiException(AiErrorKind.MissingApiKey),
        };
        return stream.StreamAsync(_http, prompt, history, options, ct);
    }

    /// <summary>Convenience: collects the whole response.</summary>
    public async Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
    {
        var output = new System.Text.StringBuilder();
        await foreach (var delta in StreamAsync(prompt, Array.Empty<Message>(), ct))
            output.Append(delta);
        return output.ToString();
    }
}
