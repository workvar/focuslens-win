using FocusLens.Core.Meetings.Detection;
using FocusLens.Platform.Windows.Capture;
using FocusLens.Platform.Windows.Native;

namespace FocusLens.Platform.Windows.Shell;

/// <summary>Feeds meeting detection with browser window titles and the focused tab's URL.</summary>
public sealed class WindowsBrowserInspector : IBrowserInspector
{
    private readonly BrowserUrlReader _urlReader = new();

    public bool IsBrowser(string appId) => BrowserCatalog.IsBrowser(appId);

    public IReadOnlyList<string> TabUrls(string appId)
    {
        // Only the focused tab's URL is readable, so report it when this browser is in front.
        var hwnd = User32.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return Array.Empty<string>();
        User32.GetWindowThreadProcessId(hwnd, out var pid);
        if (ProcessInfo.AppIdOf(pid) != appId) return Array.Empty<string>();
        var url = _urlReader.ReadUrl(hwnd);
        return url is null ? Array.Empty<string>() : new[] { url };
    }

    public IReadOnlyList<string> WindowTitles(int pid)
    {
        var titles = new List<string>();
        User32.EnumWindows((hwnd, _) =>
        {
            User32.GetWindowThreadProcessId(hwnd, out var windowPid);
            if (windowPid == (uint)pid && User32.IsWindowVisible(hwnd))
            {
                var title = User32.GetTitle(hwnd);
                if (title.Length > 0) titles.Add(title);
            }
            return true;
        }, IntPtr.Zero);
        return titles;
    }
}
