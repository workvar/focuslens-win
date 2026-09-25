using FocusLens.Core.Chroma;
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

        if (!allowed.IsOn(TrackingItem.SendScreenTextToAi))
        {
            context.RetrievalNote = "  Sharing on-screen text with the AI is switched off in Settings, so no app text can be searched.";
            return context;
        }

        try
        {
            await ScreenContextBuilder.FillAsync(
                new ScreenTextRepository(_repository.Database), context, question, start, end);
        }
        catch { /* screen text is optional context */ }

        // Same privacy gate as screen text: the index is built from it.
        if (ChromaIndexService.SemanticSearchEnabled)
            context.SemanticMatches = ChromaSearch.Render(await ChromaSearch.SearchAsync(question));
        return context;
    }
}
