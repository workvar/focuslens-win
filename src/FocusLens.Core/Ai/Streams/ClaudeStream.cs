using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using FocusLens.Core.Models;

namespace FocusLens.Core.Ai.Streams;

/// <summary>Anthropic Messages API, server-sent events.</summary>
public sealed class ClaudeStream : IChatStream
{
    private readonly string _apiKey;

    public ClaudeStream(string apiKey) => _apiKey = apiKey;

    public async IAsyncEnumerable<string> StreamAsync(
        HttpClient http, string prompt, IReadOnlyList<Message> history, AiRequestOptions options,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_apiKey)) throw new AiException(AiErrorKind.MissingApiKey);

        var body = new Dictionary<string, object>
        {
            ["model"] = ChatStreamHelpers.ChosenModel(options.Model, "claude-sonnet-4-6"),
            ["max_tokens"] = options.MaxTokens ?? 1024,
            ["stream"] = true,
            ["messages"] = ChatStreamHelpers.ChatMessages(history, prompt),
        };
        if (options.Temperature is { } temperature) body["temperature"] = temperature;
        // Off by default on Sonnet 4.6, and required to keep later models from spending a short
        // max_tokens budget on hidden reasoning.
        if (options.DisableThinking) body["thinking"] = new Dictionary<string, object> { ["type"] = "disabled" };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        using var response = await ChatStreamHelpers.SendAsync(http, request, ct);
        ChatStreamHelpers.CheckStatus(response);

        await foreach (var line in ChatStreamHelpers.ReadLinesAsync(response, ct))
        {
            var json = ChatStreamHelpers.SsePayload(line);
            if (json is null) continue;
            using var doc = ChatStreamHelpers.TryParse(json);
            if (doc is null) continue;

            var root = doc.RootElement;
            if (root.TryGetProperty("type", out var type) && type.GetString() == "content_block_delta"
                && root.TryGetProperty("delta", out var delta)
                && delta.TryGetProperty("text", out var text))
            {
                var value = text.GetString();
                if (!string.IsNullOrEmpty(value)) yield return value;
            }
        }
    }
}
