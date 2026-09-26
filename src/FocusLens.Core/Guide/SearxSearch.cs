using System.Text.Json;

namespace FocusLens.Core.Guide;

/// <summary>
/// A SearXNG instance the user chose (their own, or a public one that allows JSON). Free and keyless, and
/// the user decides who sees the query. Needs "json" enabled under search.formats on that instance.
/// </summary>
public sealed class SearxSearch : IGuideWebSearch
{
    private const int Limit = 5;
    private readonly string _baseUrl;
    private readonly HttpClient _http;

    public SearxSearch(string baseUrl, HttpClient? http = null)
    {
        _baseUrl = baseUrl.Trim().TrimEnd('/');
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
    }

    public async Task<IReadOnlyList<GuideSearchResult>> SearchAsync(string query, CancellationToken ct)
    {
        if (!Uri.TryCreate(_baseUrl, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
            return Array.Empty<GuideSearchResult>();
        try
        {
            var json = await _http.GetStringAsync($"{_baseUrl}/search?q={Uri.EscapeDataString(query)}&format=json", ct);
            return Parse(json, Limit);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return Array.Empty<GuideSearchResult>();
        }
    }

    public static IReadOnlyList<GuideSearchResult> Parse(string json, int limit)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("results", out var items) || items.ValueKind != JsonValueKind.Array)
                return Array.Empty<GuideSearchResult>();
            return items.EnumerateArray().Take(limit).Select(item => new GuideSearchResult(
                Str(item, "title"), Trunc(Str(item, "content")), Str(item, "url")))
                .Where(r => r.Title.Length > 0).ToList();
        }
        catch (JsonException) { return Array.Empty<GuideSearchResult>(); }
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static string Trunc(string s) => s.Length <= DuckDuckGoParser.SnippetLimit ? s : s[..(DuckDuckGoParser.SnippetLimit - 1)].TrimEnd() + "…";
}
