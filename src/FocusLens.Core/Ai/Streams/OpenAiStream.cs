using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using FocusLens.Core.Models;

namespace FocusLens.Core.Ai.Streams;

/// <summary>OpenAI chat completions, server-sent events.</summary>
public sealed class OpenAiStream : IChatStream
{
    private readonly string _apiKey;

    public OpenAiStream(string apiKey) => _apiKey = apiKey;

    public async IAsyncEnumerable<string> StreamAsync(
        HttpClient http, string prompt, IReadOnlyList<Message> history, AiRequestOptions options,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_apiKey)) throw new AiException(AiErrorKind.MissingApiKey);

        var body = new Dictionary<string, object>
        {
            ["model"] = "gpt-4o-mini",
            ["stream"] = true,
            ["messages"] = ChatStreamHelpers.ChatMessages(history, prompt),
        };
        if (options.MaxTokens is { } maxTokens) body["max_tokens"] = maxTokens;
        if (options.Temperature is { } temperature) body["temperature"] = temperature;

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await ChatStreamHelpers.SendAsync(http, request, ct);
        ChatStreamHelpers.CheckStatus(response);

        await foreach (var line in ChatStreamHelpers.ReadLinesAsync(response, ct))
        {
            var json = ChatStreamHelpers.SsePayload(line);
            if (json is null) continue;
            using var doc = ChatStreamHelpers.TryParse(json);
            if (doc is null) continue;

            if (doc.RootElement.TryGetProperty("choices", out var choices)
                && choices.GetArrayLength() > 0
                && choices[0].TryGetProperty("delta", out var delta)
                && delta.TryGetProperty("content", out var content))
            {
                var value = content.GetString();
                if (!string.IsNullOrEmpty(value)) yield return value;
            }
        }
    }
}
