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

        var model = ChatStreamHelpers.ChosenModel(options.Model, "claude-sonnet-4-6");
        var body = new Dictionary<string, object>
        {
            ["model"] = model,
            ["max_tokens"] = options.MaxTokens ?? 1024,
            ["stream"] = true,
            ["messages"] = AiImageAttachment.Messages(history, prompt, options.ImagePng, AiImageAttachment.Style.Anthropic),
        };
        // Sonnet 4.6 already thinks off. Sending thinking next to temperature (what Guide and
        // Focus set) is a 400. Chat omits both and succeeds. Sonnet 5 thinks unless told not to,
        // and rejects a non-default temperature.
        if (options.DisableThinking)
        {
            if (ChatStreamHelpers.ClaudeThinkingDefaultsOn(model))
                body["thinking"] = new Dictionary<string, object> { ["type"] = "disabled" };
        }
        else if (options.Temperature is { } temperature)
        {
            body["temperature"] = temperature;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        using var response = await ChatStreamHelpers.SendAsync(http, request, ct);
        await ChatStreamHelpers.EnsureSuccessAsync(response, ct);

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
