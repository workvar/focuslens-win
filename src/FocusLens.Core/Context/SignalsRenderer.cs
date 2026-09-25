namespace FocusLens.Core.Context;

/// <summary>Turns activity signals into the text block the AI prompt embeds.</summary>
public static class SignalsRenderer
{
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["app_launch"] = "opened",
        ["app_quit"] = "quit",
        ["session_active"] = "screen unlocked",
        ["session_inactive"] = "screen locked",
        ["system_sleep"] = "PC went to sleep",
        ["system_wake"] = "PC woke",
    };

    public static string Render(ActivitySignals signals)
    {
        if (signals.IsEmpty) return "";
        var lines = new List<string>();

        if (signals.Input.Count > 0)
        {
            lines.Add("  Input activity (counts only, no content):");
            lines.AddRange(signals.Input.Select(i =>
                $"    - {i.AppName}: {i.Keys} keystrokes, {i.Clicks} clicks, {i.Scrolls} scrolls"));
        }
        if (signals.Documents.Count > 0)
        {
            lines.Add("  Files open in the focused window:");
            lines.AddRange(signals.Documents.Select(d =>
                $"    - {d.Path} ({d.AppName}, last {d.LastSeen.ToLocalTime():MMM d HH:mm})"));
        }
        if (signals.CopiesByApp.Count > 0)
        {
            var copies = string.Join(", ", signals.CopiesByApp.OrderByDescending(kv => kv.Value)
                .Select(kv => $"{kv.Key}: {kv.Value}"));
            lines.Add($"  Copy actions by app (content never recorded): {copies}");
        }
        if (signals.Events.Count > 0)
        {
            lines.Add("  System events, newest first:");
            foreach (var e in signals.Events.Take(20))
            {
                var what = Labels.GetValueOrDefault(e.Kind, e.Kind);
                var name = e.Detail is null ? "" : " " + e.Detail;
                lines.Add($"    - {e.Timestamp.ToLocalTime():MMM d HH:mm} {what}{name}");
            }
        }
        return string.Join("\n", lines);
    }
}
