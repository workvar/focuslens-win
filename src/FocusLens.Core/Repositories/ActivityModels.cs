namespace FocusLens.Core.Repositories;

public sealed record CategorySegment(string Id, string Category, string ColorHex, int Seconds);

public sealed record HourlyBucket(string Id, int Hour, DateTime Date, IReadOnlyList<CategorySegment> Segments)
{
    public int TotalSeconds => Segments.Sum(s => s.Seconds);
}

public sealed record ActivityItem(string Label, int Seconds);

public sealed record AppActivityDetail(
    string Id, string Name, string BundleId, int TotalSeconds,
    IReadOnlyList<ActivityItem> Titles, IReadOnlyList<ActivityItem> Urls);
