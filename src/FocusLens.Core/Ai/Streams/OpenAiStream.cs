using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using FocusLens.Core.Models;

namespace FocusLens.Core.Ai.Streams;

public enum ChatCompletionsKind { OpenAi, Nvidia, DeepSeek }

/// <summary>OpenAI-compatible chat completions (OpenAI, NVIDIA NIM, DeepSeek), server-sent events.</summary>
public sealed class OpenAiStream : IChatStream
{
    private readonly string _apiKey;
    private readonly string _endpoint;
    private readonly string _defaultModel;
    private readonly ChatCompletionsKind _kind;

    public OpenAiStream(string apiKey)
        : this(apiKey, "https://api.openai.com/v1/chat/completions", "gpt-4o-mini", ChatCompletionsKind.OpenAi) { }

    public OpenAiStream(string apiKey, string endpoint, string defaultModel, ChatCompletionsKind kind)
    {
        _apiKey = apiKey;
        _endpoint = endpoint;
        _defaultModel = defaultModel;
        _kind = kind;
    }

    public async IAsyncEnumerable<string> StreamAsync(
        HttpClient http, string prompt, IReadOnlyList<Message> history, AiRequestOptions options,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_apiKey)) throw new AiException(AiErrorKind.MissingApiKey);

        var model = ChatStreamHelpers.ChosenModel(options.Model, _defaultModel);
        var reasoning = ChatStreamHelpers.IsReasoningModel(model);
        // NVIDIA's DeepSeek builds hang unless thinking is turned on in the request.
        var nvidiaDeepSeek = _kind == ChatCompletionsKind.Nvidia
            && model.Contains("deepseek", StringComparison.OrdinalIgnoreCase);
        var body = new Dictionary<string, object>
        {
            ["model"] = model,
            ["stream"] = true,
            ["messages"] = ChatStreamHelpers.ChatMessages(history, prompt),
        };
        if (options.MaxTokens is { } maxTokens)
            body[reasoning ? "max_completion_tokens" : "max_tokens"] = maxTokens;
        if (options.Temperature is { } temperature && !reasoning && !nvidiaDeepSeek)
            body["temperature"] = temperature;
        if (options.DisableThinking && reasoning) body["reasoning_effort"] = "none";
        // DeepSeek thinks unless told not to. Guide and Focus set this so a short answer
        // is not spent on a chain of thought. Chat leaves it at the default.
        if (_kind == ChatCompletionsKind.DeepSeek && options.DisableThinking)
            body["thinking"] = new Dictionary<string, object> { ["type"] = "disabled" };
        if (nvidiaDeepSeek)
            body["chat_template_kwargs"] = new Dictionary<string, object>
            {
                ["enable_thinking"] = true,
                ["thinking"] = true,
            };

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await ChatStreamHelpers.SendAsync(http, request, ct);
        await ChatStreamHelpers.EnsureSuccessAsync(response, ct);

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
