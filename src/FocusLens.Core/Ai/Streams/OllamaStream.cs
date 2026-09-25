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
        HttpClient http, string prompt, IReadOnlyList<Message> history,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (_model.Length == 0) throw new AiException(AiErrorKind.MissingOllamaModel);
        if (!Uri.TryCreate($"{_host}/api/chat", UriKind.Absolute, out var url))
            throw new AiException(AiErrorKind.Api, $"Invalid Ollama host: {_host}");

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                model = _model,
                stream = true,
                messages = ChatStreamHelpers.ChatMessages(history, prompt),
            }),
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
                throw new AiException(AiErrorKind.OllamaModelMissing, _model);
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
}
