using FocusLens.Core.Models;
using FocusLens.Platform.Windows.Native;
using FocusLens.Platform.Windows.Shell;

namespace FocusLens.Platform.Windows.Capture;

/// <summary>Lists real, visible top-level windows (skips tool windows, minimised and tiny ones).</summary>
public static class WindowInventory
{
    public static List<OpenWindow> VisibleWindows(uint excludingPid)
    {
        var result = new List<OpenWindow>();
        var seenPidTitle = new HashSet<(uint, string)>();

        User32.EnumWindows((hwnd, _) =>
        {
            if (!User32.IsWindowVisible(hwnd) || User32.IsIconic(hwnd)) return true;
            if ((User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE).ToInt64() & User32.WS_EX_TOOLWINDOW) != 0) return true;
            if (!User32.GetWindowRect(hwnd, out var rect) || rect.Width <= 80 || rect.Height <= 80) return true;

            var title = User32.GetTitle(hwnd);
            if (title.Length == 0) return true;

            User32.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0 || pid == excludingPid) return true;
            if (!seenPidTitle.Add((pid, title))) return true;

            var appId = ProcessInfo.AppIdOf(pid);
            if (appId is null || appId == "explorer.exe" && title == "Program Manager") return true;

            result.Add(new OpenWindow(hwnd, pid, appId, ProcessInfo.AppNameOf(pid, appId), title));
            return true;
        }, IntPtr.Zero);

        return result;
    }
}
