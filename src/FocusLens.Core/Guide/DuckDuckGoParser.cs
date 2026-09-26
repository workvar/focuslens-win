using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FocusLens.Core.Guide;

/// <summary>
/// Pure parsers for DuckDuckGo's two free endpoints, kept apart from the network code so they can be
/// tested on saved responses. Mirrors DuckDuckGoParser.swift.
/// </summary>
public static partial class DuckDuckGoParser
{
    public const int SnippetLimit = 240;

    [GeneratedRegex("""<a[^>]*class="[^"]*result__a[^"]*"[^>]*href="([^"]+)"[^>]*>(.*?)</a>""", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex TitleLink();

    [GeneratedRegex("""class="[^"]*result__snippet[^"]*"[^>]*>(.*?)</(?:a|td|div)>""", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Snippet();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    /// <summary>Results from html.duckduckgo.com. A challenge or empty page yields none.</summary>
    public static IReadOnlyList<GuideSearchResult> ParseHtml(string html, int limit)
    {
        var titles = TitleLink().Matches(html);
        var snippets = Snippet().Matches(html);
        var results = new List<GuideSearchResult>();
        for (var i = 0; i < titles.Count && results.Count < limit; i++)
        {
            var url = ResolveRedirect(titles[i].Groups[1].Value);
            var title = Clean(titles[i].Groups[2].Value);
            var snippet = i < snippets.Count ? Clean(snippets[i].Groups[1].Value) : "";
            if (title.Length == 0 || IsAd(url)) continue;
            results.Add(new GuideSearchResult(title, Trim(snippet), url));
        }
        return results;
    }

    /// <summary>The Instant Answer API: an abstract plus related topics. Encyclopedic, not how-to, but reliable.</summary>
    public static IReadOnlyList<GuideSearchResult> ParseInstantAnswer(string json, int limit)
    {
        var results = new List<GuideSearchResult>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var abstractText = Text(root, "AbstractText");
            if (abstractText.Length > 0)
                results.Add(new GuideSearchResult(Text(root, "Heading"), Trim(abstractText), Text(root, "AbstractURL")));

            if (root.TryGetProperty("RelatedTopics", out var related) && related.ValueKind == JsonValueKind.Array)
                foreach (var topic in related.EnumerateArray())
                {
                    if (results.Count >= limit) break;
                    var text = Text(topic, "Text");
                    if (text.Length == 0) continue;
                    results.Add(new GuideSearchResult(text.Split(" - ")[0], Trim(text), Text(topic, "FirstURL")));
                }
        }
        catch (JsonException)
        {
            // Not JSON (a challenge page, say): no results.
        }
        return results.Take(limit).ToList();
    }

    /// <summary>Result links are redirects: //duckduckgo.com/l/?uddg=REAL_URL&amp;rut=... Returns REAL_URL.</summary>
    public static string ResolveRedirect(string href)
    {
        href = WebUtility.HtmlDecode(href);
        var marker = href.IndexOf("uddg=", StringComparison.Ordinal);
        if (marker < 0) return href.StartsWith("//") ? "https:" + href : href;
        var end = href.IndexOf('&', marker);
        var encoded = end < 0 ? href[(marker + 5)..] : href[(marker + 5)..end];
        return Uri.UnescapeDataString(encoded);
    }

    private static bool IsAd(string url) => url.Contains("duckduckgo.com/y.js", StringComparison.Ordinal);

    private static string Clean(string html) =>
        Regex.Replace(WebUtility.HtmlDecode(Tags().Replace(html, "")), @"\s+", " ").Trim();

    private static string Trim(string text) =>
        text.Length <= SnippetLimit ? text : text[..(SnippetLimit - 1)].TrimEnd() + "…";

    private static string Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? "" : "";
}
