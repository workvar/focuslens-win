using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using FocusLens.Core.Models;

namespace FocusLens.Core.Ai.Streams;

/// <summary>Local Ollama server, newline-delimited JSON.</summary>
public sealed class OllamaStream : IChatStream
{
    private readonly string _host;
    private readonly string _model;

    public OllamaStream(string host, string model)
    {
        _host = host.Trim().TrimEnd('/');
        _model = model.Trim();
    }

    public async IAsyncEnumerable<string> StreamAsync(
        HttpClient http, string prompt, IReadOnlyList<Message> history, AiRequestOptions options,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var model = (options.OllamaModel ?? _model).Trim();
        if (model.Length == 0) throw new AiException(AiErrorKind.MissingOllamaModel);
        if (!Uri.TryCreate($"{_host}/api/chat", UriKind.Absolute, out var url))
            throw new AiException(AiErrorKind.Api, $"Invalid Ollama host: {_host}");

        var body = new Dictionary<string, object>
        {
            ["model"] = model,
            ["stream"] = true,
            ["messages"] = AiImageAttachment.Messages(history, prompt, options.ImagePng, AiImageAttachment.Style.Ollama),
        };
        Apply(options, body);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException)
        {
            throw new AiException(AiErrorKind.OllamaUnreachable, _host);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new AiException(AiErrorKind.OllamaModelMissing, model);
            ChatStreamHelpers.CheckStatus(response);

            await foreach (var line in ChatStreamHelpers.ReadLinesAsync(response, ct))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                using var doc = ChatStreamHelpers.TryParse(line.Trim());
                if (doc is null) continue;
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var error) && error.ValueKind == System.Text.Json.JsonValueKind.String)
                    throw new AiException(AiErrorKind.Api, error.GetString());

                if (root.TryGetProperty("message", out var message)
                    && message.TryGetProperty("content", out var content))
                {
                    var value = content.GetString();
                    if (!string.IsNullOrEmpty(value)) yield return value;
                }
                if (root.TryGetProperty("done", out var done) && done.ValueKind == System.Text.Json.JsonValueKind.True)
                    yield break;
            }
        }
    }

    /// <summary>
    /// Ollama's names for the shared request options.
    ///   num_predict  hard cap on generated tokens
    ///   think=false  reasoning models (qwen3, deepseek-r1) answer directly instead of
    ///                generating hundreds of hidden tokens first
    ///   keep_alive   keeps the model in memory between short requests
    /// </summary>
    internal static void Apply(AiRequestOptions options, Dictionary<string, object> body)
    {
        var modelOptions = new Dictionary<string, object>();
        if (options.MaxTokens is { } maxTokens) modelOptions["num_predict"] = maxTokens;
        if (options.Temperature is { } temperature) modelOptions["temperature"] = temperature;
        if (modelOptions.Count > 0) body["options"] = modelOptions;
        if (options.DisableThinking) body["think"] = false;
        if (options.KeepAlive is { } keepAlive) body["keep_alive"] = keepAlive;
    }
}
