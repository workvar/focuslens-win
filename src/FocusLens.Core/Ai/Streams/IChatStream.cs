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
    /// <summary>History plus the new prompt as role/content pairs.</summary>
    public static List<object> ChatMessages(IReadOnlyList<Message> history, string prompt)
    {
        var messages = history
            .Select(m => (object)new { role = m.RoleEnum == MessageRole.Assistant ? "assistant" : "user", content = m.ContentMd })
            .ToList();
        messages.Add(new { role = "user", content = prompt });
        return messages;
    }

    public static void CheckStatus(HttpResponseMessage response)
    {
        var code = (int)response.StatusCode;
        if (code is >= 200 and < 300) return;
        throw code switch
        {
            401 or 403 => new AiException(AiErrorKind.Unauthorized),
            429 => new AiException(AiErrorKind.RateLimited),
            >= 500 and < 600 => new AiException(AiErrorKind.ServerError, code.ToString()),
            _ => new AiException(AiErrorKind.Api, $"HTTP {code}"),
        };
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
