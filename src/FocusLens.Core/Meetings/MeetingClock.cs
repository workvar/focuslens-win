namespace FocusLens.Core.Meetings;

/// <summary>Elapsed-time maths that excludes paused time.</summary>
public static class MeetingClock
{
    public static TimeSpan Elapsed(MeetingStatusSnapshot snapshot, DateTime? now = null)
    {
        if (snapshot.StartedAt is not { } startedAt) return TimeSpan.Zero;
        var at = now ?? DateTime.UtcNow;
        var pausedNow = snapshot.PausedSince is { } since ? at - since : TimeSpan.Zero;
        var raw = at - startedAt - snapshot.PausedTotal - pausedNow;
        return raw < TimeSpan.Zero ? TimeSpan.Zero : raw;
    }

    public static string Format(TimeSpan interval)
    {
        var total = (int)Math.Max(0, interval.TotalSeconds);
        var h = total / 3600;
        var m = total % 3600 / 60;
        var s = total % 60;
        return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m}:{s:00}";
    }

    public static string Spoken(TimeSpan interval)
    {
        var total = (int)Math.Max(0, interval.TotalSeconds);
        var h = total / 3600;
        var m = total % 3600 / 60;
        var s = total % 60;
        var parts = new List<string>();
        if (h > 0) parts.Add($"{h} hour{(h == 1 ? "" : "s")}");
        if (m > 0) parts.Add($"{m} minute{(m == 1 ? "" : "s")}");
        if (parts.Count == 0 || s > 0) parts.Add($"{s} second{(s == 1 ? "" : "s")}");
        return string.Join(", ", parts);
    }
}
