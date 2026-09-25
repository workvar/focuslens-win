using System.Text;
using System.Text.Json;

namespace FocusLens.Core.Ai;

/// <summary>Small client for the local Ollama HTTP API: list installed models and pull new ones.</summary>
public sealed class OllamaClient
{
    private static readonly HttpClient Short = new() { Timeout = TimeSpan.FromSeconds(4) };
    private static readonly HttpClient Long = new() { Timeout = Timeout.InfiniteTimeSpan };

    private readonly string _host;

    public OllamaClient(string host) => _host = host.Trim().TrimEnd('/');

    /// <summary>Installed model names, sorted. Empty when Ollama is not running.</summary>
    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await Short.GetAsync($"{_host}/api/tags", ct);
            if (!response.IsSuccessStatusCode) return Array.Empty<string>();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("models", out var models)) return Array.Empty<string>();
            return models.EnumerateArray()
                .Select(m => m.TryGetProperty("name", out var n) ? n.GetString() : null)
                .OfType<string>()
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or UriFormatException)
        {
            return Array.Empty<string>();
        }
    }

    public async Task<bool> IsRunningAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await Short.GetAsync($"{_host}/api/version", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            return false;
        }
    }

    /// <summary>Downloads a model, reporting 0..1 progress and a status line. Throws on failure.</summary>
    public async Task PullAsync(string model, IProgress<(double Fraction, string Status)> progress, CancellationToken ct)
    {
        var body = new StringContent(JsonSerializer.Serialize(new { name = model, stream = true }), Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_host}/api/pull") { Content = body };
        using var response = await Long.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(ct));
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (line.Length == 0) continue;
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var error))
                throw new InvalidOperationException(error.GetString() ?? "Ollama could not pull the model.");

            var status = root.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
            var fraction = root.TryGetProperty("total", out var t) && t.GetInt64() > 0 && root.TryGetProperty("completed", out var c)
                ? (double)c.GetInt64() / t.GetInt64()
                : 0;
            progress.Report((fraction, status));
        }
    }
}
