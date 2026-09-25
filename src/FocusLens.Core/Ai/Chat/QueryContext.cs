using FocusLens.Core.Repositories;

namespace FocusLens.Core.Ai.Chat;

public sealed record AppUsageSummary(string Name, int Seconds);

/// <summary>Everything the prompt needs about the period the user asked about.</summary>
public sealed class QueryContext
{
    public string Question { get; init; } = "";
    public string PeriodLabel { get; init; } = "";
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int SummaryCount { get; init; }
    public IReadOnlyDictionary<string, int> CategoryTotals { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<AppUsageSummary> TopApps { get; init; } = Array.Empty<AppUsageSummary>();
    public IReadOnlyList<(string Date, double Score)> DailyFocusScores { get; init; } = Array.Empty<(string, double)>();
    public IReadOnlyList<AppActivityDetail> AppDetails { get; init; } = Array.Empty<AppActivityDetail>();
    public string SessionContext { get; set; } = "";
    public string OpenTabs { get; set; } = "";
    public string Signals { get; set; } = "";
    /// <summary>What the on-screen text search looked for and found, so the model can say so plainly.</summary>
    public string RetrievalNote { get; set; } = "";
}
