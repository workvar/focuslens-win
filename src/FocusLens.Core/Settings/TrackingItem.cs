namespace FocusLens.Core.Settings;

/// <summary>One switch per kind of data FocusLens may collect.</summary>
public enum TrackingItem
{
    ActiveApp,
    WindowTitles,
    BrowserUrls,
    IdleDetection,
    FocusedScreenText,
    BackgroundWindowText,
    BrowserTabs,
    InputActivity,
    DocumentPaths,
    ClipboardActivity,
    AppLaunches,
    ScreenState,
    SendScreenTextToAi,
    SendSignalsToAi,
}

public static class TrackingItemExtensions
{
    /// <summary>The key stored in tracking.json (camelCase of the enum name).</summary>
    public static string Key(this TrackingItem item)
    {
        var name = item.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>An item that has no effect unless another one is on.</summary>
    public static TrackingItem? Requires(this TrackingItem item) => item switch
    {
        TrackingItem.WindowTitles or TrackingItem.BrowserUrls
            or TrackingItem.IdleDetection or TrackingItem.DocumentPaths => TrackingItem.ActiveApp,
        _ => null,
    };
}
