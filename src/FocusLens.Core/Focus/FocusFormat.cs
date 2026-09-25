using System.Globalization;

namespace FocusLens.Core.Focus;

/// <summary>Small text helpers shared by the focus screens.</summary>
public static class FocusFormat
{
    /// <summary>"1h 5m", "4m 10s", "35s".</summary>
    public static string Duration(double seconds)
    {
        var total = Math.Max(0, (int)Math.Round(seconds));
        var hours = total / 3600;
        var minutes = total % 3600 / 60;
        var secs = total % 60;
        if (hours > 0) return $"{hours}h {minutes}m";
        if (minutes > 0) return $"{minutes}m {secs}s";
        return $"{secs}s";
    }

    /// <summary>"3 visits", "1 visit".</summary>
    public static string Plural(int count, string noun) => $"{count} {noun}{(count == 1 ? "" : "s")}";

    /// <summary>"Sep 25, 2026, 5:30 PM". The time uses the standard short-time format, so it follows the PC's 12 or 24 hour setting.</summary>
    public static string Day(DateTime utc)
    {
        var local = utc.ToLocalTime();
        return $"{local.ToString("MMM d, yyyy", CultureInfo.CurrentCulture)}, {local.ToString("t", CultureInfo.CurrentCulture)}";
    }

    public static string Clock(DateTime utc) => utc.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);

    /// <summary>"mm:ss", or "h:mm:ss" from an hour up.</summary>
    public static string Remaining(TimeSpan left)
    {
        var total = Math.Max(0, (int)Math.Ceiling(left.TotalSeconds));
        var hours = total / 3600;
        var minutes = total % 3600 / 60;
        var seconds = total % 60;
        return hours > 0 ? $"{hours}:{minutes:00}:{seconds:00}" : $"{minutes:00}:{seconds:00}";
    }

    /// <summary>Whole minutes, for reduced effects: "12 min", "&lt;1 min".</summary>
    public static string RemainingMinutes(TimeSpan left)
    {
        var minutes = Math.Max(0, (int)Math.Ceiling(left.TotalMinutes));
        return minutes <= 1 ? "<1 min" : $"{minutes} min";
    }

    /// <summary>"42m" for the tray.</summary>
    public static string ShortRemaining(TimeSpan left)
    {
        var minutes = (int)Math.Ceiling(left.TotalMinutes);
        return minutes <= 1 ? "<1m" : $"{minutes}m";
    }
}
