namespace FocusLens.Core.Focus;

/// <summary>A few plain-language observations about a finished session.</summary>
public static class FocusRecordInsights
{
    public static List<string> Insights(this FocusSessionRecord r)
    {
        var lines = new List<string>();

        if (r.FocusedSeconds + r.DistractedSeconds > 0)
            lines.Add($"You spent {FocusFormat.Duration(r.FocusedSeconds)} on topic and {FocusFormat.Duration(r.DistractedSeconds)} off it.");

        var top = r.TopDistractions().FirstOrDefault();
        if (top is not null)
        {
            var share = r.DistractedSeconds > 0 ? (int)Math.Round(top.Seconds / r.DistractedSeconds * 100) : 0;
            lines.Add($"{top.Label} cost you the most: {FocusFormat.Duration(top.Seconds)} across {FocusFormat.Plural(top.Count, "visit")}, {share}% of your drift.");
        }
        else
        {
            lines.Add("No distractions were recorded. Nicely done.");
        }

        var count = r.DistractionCount();
        if (count > 0)
            lines.Add($"You came back on your own {r.ReturnedOnOwnCount()} of {FocusFormat.Plural(count, "time")} before a nudge.");
        return lines;
    }
}
