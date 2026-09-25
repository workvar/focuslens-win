using FocusLens.Core.Context;
using FocusLens.Core.Repositories;
using FocusLens.Core.Settings;

namespace FocusLens.Core.Ai.Chat;

/// <summary>Collects the data the prompt needs, honouring the user's AI-sharing switches.</summary>
public sealed class ContextBuilder
{
    private readonly ActivityRepository _repository;

    public ContextBuilder(ActivityRepository repository) => _repository = repository;

    public async Task<QueryContext> BuildAsync(string question)
    {
        var (start, end, label) = DateRangeDetector.Detect(question);
        var summaries = await _repository.FetchSummariesAsync(start, end);

        IReadOnlyList<AppActivityDetail> details;
        try { details = await _repository.FetchAppActivityDetailsAsync(start, end); }
        catch { details = Array.Empty<AppActivityDetail>(); }

        var categoryTotals = new Dictionary<string, int>();
        var appTotals = new Dictionary<string, (string Name, int Seconds)>();
        var scores = new List<(string, double)>();

        foreach (var summary in summaries)
        {
            scores.Add((summary.Date, summary.FocusScore));
            foreach (var category in summary.CategoryTotals)
                categoryTotals[category.Name] = categoryTotals.GetValueOrDefault(category.Name) + category.Seconds;
            foreach (var app in summary.TopApps.Take(10))
            {
                var current = appTotals.GetValueOrDefault(app.BundleId);
                appTotals[app.BundleId] = (app.Name, current.Seconds + app.Seconds);
            }
        }

        var context = new QueryContext
        {
            Question = question,
            PeriodLabel = label,
            StartDate = start,
            EndDate = end,
            SummaryCount = summaries.Count,
            CategoryTotals = categoryTotals,
            TopApps = appTotals.Values.OrderByDescending(v => v.Seconds).Take(10)
                .Select(v => new AppUsageSummary(v.Name, v.Seconds)).ToList(),
            DailyFocusScores = scores,
            AppDetails = details,
        };

        var allowed = TrackingSettings.Load();
        if (allowed.IsOn(TrackingItem.SendSignalsToAi))
        {
            try
            {
                var signals = await new SignalsRepository(_repository.Database).SignalsAsync(start, end);
                context.Signals = SignalsRenderer.Render(signals);
            }
            catch { /* signals are optional context */ }
        }

        if (!allowed.IsOn(TrackingItem.SendScreenTextToAi)) return context;

        var screen = new ScreenTextRepository(_repository.Database);
        try
        {
            var snapshots = await screen.SnapshotsAsync(start, end);
            if (snapshots.Count > 0)
            {
                var urls = await screen.UrlsAsync(start, end);
                var notes = SessionBuilder.Build(snapshots, urls);
                context.SessionContext = SessionRetriever.Render(notes, question);
            }
            var tabs = await screen.OpenTabsAsync(start, end);
            context.OpenTabs = TabsRenderer.Render(tabs, question);
        }
        catch { /* screen text is optional context */ }
        return context;
    }
}
