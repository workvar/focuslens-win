using System.Runtime.CompilerServices;
using System.Text.Json;
using FocusLens.Core.Models;

namespace FocusLens.Core.Ai.Streams;

/// <summary>One provider's streaming chat protocol.</summary>
public interface IChatStream
{
    /// <param name="options">Length, temperature and reasoning limits; see <see cref="AiRequestOptions"/>.</param>
    IAsyncEnumerable<string> StreamAsync(
        HttpClient http, string prompt, IReadOnlyList<Message> history, AiRequestOptions options, CancellationToken ct);
}

internal static class ChatStreamHelpers
{
    /// <summary>A non-empty override, otherwise the provider's built-in model.</summary>
    public static string ChosenModel(string? model, string fallback)
    {
        var trimmed = model?.Trim() ?? "";
        return trimmed.Length == 0 ? fallback : trimmed;
    }

    /// <summary>gpt-5 and the o-series accept reasoning controls. gpt-4o and gpt-4.1 reject them.</summary>
    public static bool IsReasoningModel(string model)
    {
        var name = model.ToLowerInvariant();
        return name.StartsWith("gpt-5") || name.StartsWith("o1") || name.StartsWith("o3") || name.StartsWith("o4");
    }

    /// <summary>History plus the new prompt as role/content pairs.</summary>
    public static List<object> ChatMessages(IReadOnlyList<Message> history, string prompt)
    {
        var messages = history
            .Select(m => (object)new { role = m.RoleEnum == MessageRole.Assistant ? "assistant" : "user", content = m.ContentMd })
            .ToList();
        messages.Add(new { role = "user", content = prompt });
        return messages;
    }

    public static void CheckStatus(HttpResponseMessage response) => CheckStatus(response, null);

    /// <summary>On a rejected request, <paramref name="body"/> supplies the provider's own message.</summary>
    public static void CheckStatus(HttpResponseMessage response, string? body)
    {
        var code = (int)response.StatusCode;
        if (code is >= 200 and < 300) return;
        throw code switch
        {
            401 or 403 => new AiException(AiErrorKind.Unauthorized),
            429 => new AiException(AiErrorKind.RateLimited),
            >= 500 and < 600 => new AiException(AiErrorKind.ServerError, code.ToString()),
            _ => new AiException(AiErrorKind.Api, ProviderMessage(body) ?? $"HTTP {code}"),
        };
    }

    /// <summary>Reads a failed response and throws. A success response is left unread for streaming.</summary>
    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        string? body = null;
        try { body = await response.Content.ReadAsStringAsync(ct); }
        catch (Exception) when (!ct.IsCancellationRequested) { }
        CheckStatus(response, body);
    }

    /// <summary>Sonnet 5 thinks unless the request turns it off. Sonnet 4.6 is already off.</summary>
    public static bool ClaudeThinkingDefaultsOn(string model) =>
        model.Contains("sonnet-5", StringComparison.OrdinalIgnoreCase);

    /// <summary>A short reason from an error body. NVIDIA uses <c>detail</c>; others use <c>error.message</c>.</summary>
    public static string? ProviderMessage(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        var raw = body.Trim();
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            string? text = null;
            if (root.TryGetProperty("error", out var error))
            {
                text = error.ValueKind switch
                {
                    JsonValueKind.Object when error.TryGetProperty("message", out var message) => message.GetString(),
                    JsonValueKind.String => error.GetString(),
                    _ => null,
                };
            }
            if (string.IsNullOrWhiteSpace(text) && root.TryGetProperty("detail", out var detail))
                text = detail.GetString();
            if (string.IsNullOrWhiteSpace(text) && root.TryGetProperty("message", out var rootMessage))
                text = rootMessage.GetString();
            text = text?.Trim();
            if (!string.IsNullOrEmpty(text))
                return text.Length > 180 ? text[..180] + "…" : text;
        }
        catch (JsonException) { }
        return raw.Length > 180 ? raw[..180] + "…" : raw;
    }

    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient http, HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new AiException(AiErrorKind.Network, inner: ex);
        }
    }

    /// <summary>Yields each line of a streaming response body.</summary>
    public static async IAsyncEnumerable<string> ReadLinesAsync(
        HttpResponseMessage response, [EnumeratorCancellation] CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            ct.ThrowIfCancellationRequested();
            yield return line;
        }
    }

    /// <summary>Payload of an SSE "data:" line, or null for anything else or [DONE].</summary>
    public static string? SsePayload(string line)
    {
        if (!line.StartsWith("data:")) return null;
        var json = line[5..].Trim();
        return json.Length == 0 || json == "[DONE]" ? null : json;
    }

    public static JsonDocument? TryParse(string json)
    {
        try { return JsonDocument.Parse(json); }
        catch (JsonException) { return null; }
    }
}
