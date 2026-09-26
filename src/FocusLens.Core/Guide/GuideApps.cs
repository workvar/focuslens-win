namespace FocusLens.Core.Guide;

/// <summary>One app the user already has running, with a window on screen.</summary>
public sealed record GuideOpenApp(string Name, bool IsFront, bool IsBrowser);

/// <summary>
/// Which apps count as a browser. Pure, and mirrored in Mac/Sources/FocusLensApp/Guide/GuideApps.swift.
///
/// The planner is told what is already open so it never sends the user to launch something that is
/// running. "Open Chrome" when Chrome is two windows behind is the single most common wrong step,
/// and because clicking a taskbar button for an app that is already open barely changes the screen,
/// Guide used to read that as a dead control and stop.
/// </summary>
public static class GuideBrowsers
{
    private static readonly string[] Processes =
    {
        "chrome", "msedge", "firefox", "brave", "opera", "opera_gx", "vivaldi", "iexplore",
        "arc", "zen", "chromium", "browser", "waterfox", "librewolf",
    };

    private static readonly string[] NameHints =
    {
        "chrome", "edge", "firefox", "brave", "opera", "vivaldi", "arc", "zen browser",
        "chromium", "internet explorer", "browser",
    };

    public static bool IsBrowser(string appName, string? processName = null)
    {
        var process = (processName ?? string.Empty).ToLowerInvariant();
        if (process.Length > 0 && Processes.Contains(process)) return true;
        var name = appName.ToLowerInvariant();
        return NameHints.Any(hint => name == hint || name.StartsWith(hint + " ", StringComparison.Ordinal)
            || name.EndsWith(" " + hint, StringComparison.Ordinal));
    }

    /// <summary>The browser to use for a web task: the one in front, else the first running one.</summary>
    public static GuideOpenApp? Pick(IReadOnlyList<GuideOpenApp> apps) =>
        apps.FirstOrDefault(a => a.IsBrowser && a.IsFront) ?? apps.FirstOrDefault(a => a.IsBrowser);
}
