namespace FocusLens.Core.Guide;

/// <summary>
/// Waiting for the screen to finish changing.
///
/// Some steps navigate: pressing Return in an address bar, clicking a link, opening an app from the
/// taskbar. The screen then goes through several states, one of which is a blank page with nothing
/// to match. Guide used to snapshot straight after the action, find neither the next control nor
/// anything it recognised, and count that as a failure. On a slow page that is three failures in a
/// row, which was enough to end the guide.
///
/// So after a navigating step, Guide waits for the screen to stop changing before it plans again.
/// The policy is here; the waiting itself belongs to the session controller, which is the only
/// thing that can take a snapshot.
/// </summary>
public static class GuideSettle
{
    /// <summary>Give up waiting after this and plan against whatever is there.</summary>
    public static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(14);
    /// <summary>The screen counts as settled once two snapshots this far apart match.</summary>
    public static readonly TimeSpan Quiet = TimeSpan.FromSeconds(1);
    /// <summary>A navigating step gets at least this long before the first check, because a page usually keeps the old one painted for a moment.</summary>
    public static readonly TimeSpan LeadIn = TimeSpan.FromMilliseconds(600);

    /// <summary>True when this step is expected to replace what is on screen rather than change part of it.</summary>
    public static bool Navigates(GuideStep step, bool inBrowser)
    {
        var role = step.Target?.Role?.ToLowerInvariant();
        return step.Action switch
        {
            GuideAction.Open => true,
            // Typing a search and pressing Return loads a page. Typing into a form field does not.
            GuideAction.Type => inBrowser,
            GuideAction.Click => role is "link" or "taskbaritem" or "dockitem" || (inBrowser && role == "button"),
            _ => false,
        };
    }
}
