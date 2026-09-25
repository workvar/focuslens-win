namespace FocusLens.Core.Context;

/// <summary>Renders the open browser tabs most relevant to a question.</summary>
public static class TabsRenderer
{
    public static string Render(IReadOnlyList<OpenTab> tabs, string question, int limit = 25)
    {
        if (tabs.Count == 0) return "";
        var terms = TextTokens.Words(question);

        var ranked = tabs
            .Select((tab, index) => (tab, score: Score(tab, terms), index))
            .OrderByDescending(x => x.score).ThenBy(x => x.index)
            .Take(limit);

        return string.Join("\n", ranked.Select(x =>
        {
            var title = string.IsNullOrEmpty(x.tab.Title)
                ? "(untitled)"
                : x.tab.Title.Length > 90 ? x.tab.Title[..90] : x.tab.Title;
            var url = string.IsNullOrEmpty(x.tab.Url) ? "" : ": " + x.tab.Url;
            return $"  - [{x.tab.Browser}] {title}{url}";
        }));
    }

    private static int Score(OpenTab tab, List<string> terms)
    {
        var haystack = (tab.Title + " " + tab.Url).ToLowerInvariant();
        return terms.Count(haystack.Contains);
    }
}
