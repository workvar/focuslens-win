using System.Windows.Automation;
using FocusLens.Core.Models;
using FocusLens.Platform.Windows.Capture.Uia;
using FocusLens.Platform.Windows.Native;
using FocusLens.Platform.Windows.Shell;

namespace FocusLens.Platform.Windows.Capture;

/// <summary>
/// Lists open browser tabs by reading the tab strip through UI Automation. Windows exposes
/// tab titles but not each tab's URL, so background tabs carry an empty URL; the focused
/// tab's URL comes from <see cref="BrowserUrlReader"/> instead.
/// </summary>
public static class BrowserTabsReader
{
    public static List<BrowserTab> ReadAll(uint excludingPid)
    {
        var tabs = new List<BrowserTab>();
        var seenWindows = new HashSet<IntPtr>();

        User32.EnumWindows((hwnd, _) =>
        {
            if (!User32.IsWindowVisible(hwnd)) return true;
            User32.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == excludingPid || pid == 0) return true;

            var appId = ProcessInfo.AppIdOf(pid);
            if (appId is null || !BrowserCatalog.IsBrowser(appId) || !seenWindows.Add(hwnd)) return true;

            tabs.AddRange(ReadWindow(hwnd, appId));
            return true;
        }, IntPtr.Zero);

        return tabs;
    }

    private static IEnumerable<BrowserTab> ReadWindow(IntPtr hwnd, string appId)
    {
        var root = UiaSearch.FromHandle(hwnd);
        if (root is null) yield break;

        var browser = BrowserCatalog.DisplayName(appId);
        foreach (var item in UiaSearch.FindAll(root, ControlType.TabItem, limit: 60))
        {
            var title = UiaSearch.Name(item).Trim();
            if (title.Length == 0) continue;
            yield return new BrowserTab(browser, title, "", appId);
        }
    }
}
