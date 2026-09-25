namespace FocusLens.Platform.Windows.Capture;

/// <summary>Known browsers by executable name.</summary>
public static class BrowserCatalog
{
    private static readonly Dictionary<string, string> Browsers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chrome.exe"] = "Chrome",
        ["msedge.exe"] = "Edge",
        ["firefox.exe"] = "Firefox",
        ["brave.exe"] = "Brave",
        ["opera.exe"] = "Opera",
        ["vivaldi.exe"] = "Vivaldi",
        ["arc.exe"] = "Arc",
    };

    public static bool IsBrowser(string appId) => Browsers.ContainsKey(appId);

    public static string DisplayName(string appId) =>
        Browsers.TryGetValue(appId, out var name) ? name : appId;
}
