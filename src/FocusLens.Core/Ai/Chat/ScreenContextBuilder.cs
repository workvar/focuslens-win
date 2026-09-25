using FocusLens.Core.Context;
using FocusLens.Core.Repositories;

namespace FocusLens.Core.Ai.Chat;

/// <summary>
/// Fills the on-screen text parts of a QueryContext. Questions that name no period search the
/// whole retention window, and keyword hits are merged with the newest snapshots so older
/// conversations are still found.
/// </summary>
public static class ScreenContextBuilder
{
    private const int RetentionDays = 14;
    private const int RecentLimit = 800;

    public static async Task FillAsync(
        ScreenTextRepository screen, QueryContext context, string question, DateTime start, DateTime end)
    {
        var from = DateRangeDetector.HasExplicitRange(question) ? start : DateTime.Now.AddDays(-RetentionDays);
        var terms = QuestionTerms.Keywords(question);

        var recent = await screen.SnapshotsAsync(from, end, RecentLimit);
        var matched = await screen.SearchSnapshotsAsync(from, end, terms);
        var snapshots = SnapshotMerger.Merge(recent, matched);

        var notes = Array.Empty<SessionNote>() as IReadOnlyList<SessionNote>;
        if (snapshots.Count > 0)
        {
            var urls = await screen.UrlsAsync(from, end);
            notes = SessionBuilder.Build(snapshots, urls);
            context.SessionContext = SessionRetriever.Render(notes, question);
        }

        var tabs = await screen.OpenTabsAsync(from, end);
        context.OpenTabs = TabsRenderer.Render(tabs, question);

        var coverage = await screen.CoverageAsync(from, end);
        context.RetrievalNote = Describe(terms, from, end, SessionRetriever.MatchCount(notes, question), coverage);
    }

    private static string Describe(
        IReadOnlyList<string> terms, DateTime from, DateTime end, int matches, IReadOnlyList<(string App, int Count)> coverage)
    {
        var range = $"{from:MMM d} to {end:MMM d}";
        var searched = terms.Count == 0
            ? $"  No specific search words; showing recent activity from {range}."
            : matches > 0
                ? $"  Searched on-screen text from {range} for: {string.Join(", ", terms)}. Found {matches} matching sessions."
                : $"  Searched on-screen text from {range} for: {string.Join(", ", terms)}. NOTHING matched.";

        var apps = coverage.Count == 0
            ? "  No on-screen text was captured in this period."
            : "  Apps with captured on-screen text: " + string.Join(", ", coverage.Select(c => $"{c.App} ({c.Count})"));
        return searched + "\n" + apps;
    }
}
