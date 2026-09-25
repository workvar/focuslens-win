namespace FocusLens.Core.Ai.Chat;

/// <summary>Picks a time window from natural-language hints in the question.</summary>
public static class DateRangeDetector
{
    public static (DateTime Start, DateTime End, string Label) Detect(string question, DateTime? nowOverride = null)
    {
        var q = question.ToLowerInvariant();
        var now = nowOverride ?? DateTime.Now;
        var today = now.Date;

        if (q.Contains("today")) return (today, now, "today");

        if (q.Contains("yesterday"))
        {
            var start = today.AddDays(-1);
            return (start, start.AddSeconds(86399), "yesterday");
        }

        // "last week" must be checked before the broader "week" match.
        if (q.Contains("last week"))
        {
            var thisWeek = StartOfWeek(today);
            return (thisWeek.AddDays(-7), thisWeek.AddSeconds(-1), "last week");
        }
        if (q.Contains("this week") || q.Contains("week"))
            return (StartOfWeek(today), now, "this week");

        if (q.Contains("this month") || q.Contains("month"))
            return (new DateTime(today.Year, today.Month, 1), now, "this month");

        return (now.AddDays(-7), now, "the last 7 days");
    }

    /// <summary>True when the question names a period; otherwise the default 7 days was assumed.</summary>
    public static bool HasExplicitRange(string question)
    {
        var q = question.ToLowerInvariant();
        return q.Contains("today") || q.Contains("yesterday") || q.Contains("week") || q.Contains("month");
    }

    private static DateTime StartOfWeek(DateTime day)
    {
        var offset = ((int)day.DayOfWeek + 6) % 7; // Monday start
        return day.AddDays(-offset);
    }
}
