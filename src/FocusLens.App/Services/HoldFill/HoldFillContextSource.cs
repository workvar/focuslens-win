using FocusLens.App.Services.Guide;
using FocusLens.Core.Focus.Session;
using FocusLens.Core.HoldFill;
using FocusLens.Core.Models;
using FocusLens.Core.Repositories;

namespace FocusLens.App.Services.HoldFill;

/// <summary>
/// Gathers what a suggestion is based on: window titles from the last 20 minutes (already
/// filtered by the privacy rules when the agent recorded them), the Focus goal, and the text of a
/// running Guide typing step.
/// </summary>
public sealed class HoldFillContextSource
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(20);

    private readonly ActivityRepository _activity;
    private readonly FocusSessionController _focus;
    private readonly GuideSessionController _guide;

    public HoldFillContextSource(ActivityRepository activity, FocusSessionController focus, GuideSessionController guide)
    {
        _activity = activity;
        _focus = focus;
        _guide = guide;
    }

    /// <summary>Call on the UI thread: Focus and Guide state are read before the database query.</summary>
    public async Task<HoldFillContext> GetAsync()
    {
        var goal = _focus.Session?.Goal;
        var guideText = _guide.TypingText;
        if (!string.IsNullOrWhiteSpace(guideText)) return new HoldFillContext(Array.Empty<string>(), goal, guideText);

        IReadOnlyList<ActivityEvent> events;
        var now = DateTime.UtcNow;
        try { events = await _activity.FetchRawEventsAsync(now - Window, now); }
        catch { events = Array.Empty<ActivityEvent>(); }

        var titles = events
            .Reverse()
            .Where(e => !e.IsIdle && !e.AppBundleId.Contains("focuslens", StringComparison.OrdinalIgnoreCase))
            .Select(Describe)
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(HoldFillPrompt.MaxTitles)
            .ToList();
        return new HoldFillContext(titles, goal);
    }

    private static string Describe(ActivityEvent e)
    {
        var title = e.WindowTitle?.Trim() ?? "";
        return title.Length > 0 ? title : e.AppName.Trim();
    }
}
