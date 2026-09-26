using System.Net.Http.Headers;

namespace FocusLens.Core.Guide;

/// <summary>
/// DuckDuckGo, no key needed. Tries the HTML results page first, which has real web results but is
/// sometimes answered with a bot challenge, then the Instant Answer API, which is reliable but
/// only covers topics with an encyclopedia entry.
/// </summary>
public sealed class DuckDuckGoSearch : IGuideWebSearch
{
    private const int Limit = 5;
    private const string UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15";

    private readonly HttpClient _http;

    public DuckDuckGoSearch(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
    }

    public async Task<IReadOnlyList<GuideSearchResult>> SearchAsync(string query, CancellationToken ct)
    {
        var html = await GetAsync($"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}", ct);
        var results = html is null ? Array.Empty<GuideSearchResult>() : DuckDuckGoParser.ParseHtml(html, Limit);
        if (results.Count > 0) return results;

        var json = await GetAsync(
            $"https://api.duckduckgo.com/?q={Uri.EscapeDataString(query)}&format=json&no_html=1&skip_disambig=1", ct);
        return json is null ? Array.Empty<GuideSearchResult>() : DuckDuckGoParser.ParseInstantAnswer(json, Limit);
    }

    private async Task<string?> GetAsync(string url, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(UserAgent);
            using var response = await _http.SendAsync(request, ct);
            // 202 is DuckDuckGo's bot challenge: a page with no results.
            return response.StatusCode == System.Net.HttpStatusCode.OK ? await response.Content.ReadAsStringAsync(ct) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return null;
        }
    }
}
