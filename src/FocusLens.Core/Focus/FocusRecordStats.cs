namespace FocusLens.Core.Focus;

/// <summary>Counts and rankings derived from a saved session.</summary>
public static class FocusRecordStats
{
    public static double Duration(this FocusSessionRecord r) => (r.EndedAt - r.StartedAt).TotalSeconds;
    public static int DistractionCount(this FocusSessionRecord r) => r.Episodes.Count;
    public static int NudgeCount(this FocusSessionRecord r) => r.Episodes.Count(e => e.Nudged);
    public static int CloseCount(this FocusSessionRecord r) => Count(r, EpisodeOutcome.Closed);
    public static int BlockCount(this FocusSessionRecord r) => Count(r, EpisodeOutcome.Blocked);
    public static int SnoozeCount(this FocusSessionRecord r) => Count(r, EpisodeOutcome.Snoozed);

    public static int ReturnedOnOwnCount(this FocusSessionRecord r) =>
        r.Episodes.Count(e => e.Outcome == EpisodeOutcome.Returned && !e.Nudged);

    /// <summary>Sites and apps ranked by time lost.</summary>
    public static List<DistractionSummary> TopDistractions(this FocusSessionRecord r) => r.Episodes
        .GroupBy(e => e.Label)
        .Select(g => new DistractionSummary(g.Key, g.Sum(e => e.Seconds), g.Count()))
        .OrderByDescending(s => s.Seconds)
        .ToList();

    private static int Count(FocusSessionRecord r, EpisodeOutcome outcome) => r.Episodes.Count(e => e.Outcome == outcome);
}
