namespace FocusLens.Core.Guide;

/// <summary>One web result, trimmed to what a planner needs.</summary>
public sealed record GuideSearchResult(string Title, string Snippet, string Url);

public interface IGuideWebSearch
{
    /// <summary>Results for a query, or an empty list. Never throws for a network failure: search is a hint, not a requirement.</summary>
    Task<IReadOnlyList<GuideSearchResult>> SearchAsync(string query, CancellationToken ct);
}

/// <summary>
/// Tries each free source in turn and returns the first that has results. A source that is blocked,
/// slow or empty is skipped, so a failing search never stops a guide.
/// </summary>
public sealed class GuideSearchChain : IGuideWebSearch
{
    private readonly IReadOnlyList<IGuideWebSearch> _sources;

    public GuideSearchChain(IEnumerable<IGuideWebSearch> sources) => _sources = sources.ToList();

    public async Task<IReadOnlyList<GuideSearchResult>> SearchAsync(string query, CancellationToken ct)
    {
        foreach (var source in _sources)
        {
            ct.ThrowIfCancellationRequested();
            var results = await source.SearchAsync(query, ct);
            if (results.Count > 0) return results;
        }
        return Array.Empty<GuideSearchResult>();
    }
}
