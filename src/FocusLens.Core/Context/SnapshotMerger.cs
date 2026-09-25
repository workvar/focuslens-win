namespace FocusLens.Core.Context;

/// <summary>Combines the newest snapshots with keyword matches, without duplicates, oldest first.</summary>
public static class SnapshotMerger
{
    public static IReadOnlyList<ScreenTextSnapshot> Merge(
        IEnumerable<ScreenTextSnapshot> recent, IEnumerable<ScreenTextSnapshot> matched) =>
        recent.Concat(matched)
            .GroupBy(s => (s.Timestamp, s.AppId, s.Title))
            .Select(g => g.First())
            .OrderBy(s => s.Timestamp)
            .ToList();
}
