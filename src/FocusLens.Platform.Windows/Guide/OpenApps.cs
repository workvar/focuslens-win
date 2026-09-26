using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FocusLens.Core.Guide;

namespace FocusLens.Platform.Windows.Guide;

/// <summary>
/// What the user already has open, as top-level windows with a title, one entry per app.
///
/// The planner is told this so it never sends the user to launch something that is running.
/// "Open Chrome" when Chrome is two windows behind is the single most common wrong step, and because
/// clicking a taskbar button for an app that is already open barely changes the screen, Guide used
/// to read that as a dead control and stop.
/// </summary>
public static class OpenApps
{
    private static readonly string[] IgnoredClasses = { "Progman", "WorkerW", "Shell_TrayWnd" };

    /// <summary>
    /// Store apps (Settings, Photos, Mail) all run inside this host, so its process name says
    /// nothing. Their window title is the app name the user knows, so that is used instead.
    /// </summary>
    private const string StoreAppHost = "ApplicationFrameHost";

    /// <summary>Apps with a visible, titled, unowned window, the front one first. FocusLens itself is left out.</summary>
    public static IReadOnlyList<GuideOpenApp> List()
    {
        var ownPid = Environment.ProcessId;
        var foreground = GuideNative.GetForegroundWindow();
        GuideNative.GetWindowThreadProcessId(foreground, out var frontPid);
        var byName = new Dictionary<string, GuideOpenApp>(StringComparer.OrdinalIgnoreCase);

        GuideNative.EnumWindows((hwnd, _) =>
        {
            try
            {
                if (!GuideNative.IsWindowVisible(hwnd)) return true;
                if (GuideNative.GetWindow(hwnd, GuideNative.GW_OWNER) != IntPtr.Zero) return true;
                if (GuideNative.GetWindowTextLengthW(hwnd) == 0) return true;
                if (IgnoredClasses.Contains(ClassOf(hwnd))) return true;

                GuideNative.GetWindowThreadProcessId(hwnd, out var pid);
                if (pid == 0 || pid == ownPid) return true;

                var (name, process) = AppNameOf((int)pid);
                if (string.Equals(process, StoreAppHost, StringComparison.OrdinalIgnoreCase))
                    name = TitleOf(hwnd);
                if (name.Length == 0) return true;

                var isFront = pid == frontPid;
                if (byName.TryGetValue(name, out var existing) && !isFront) return true;
                byName[name] = new GuideOpenApp(name, isFront || (existing?.IsFront ?? false),
                    GuideBrowsers.IsBrowser(name, process));
            }
            catch
            {
                // A window that closed mid-walk, or a process this app may not query. Skip it.
            }
            return true;
        }, IntPtr.Zero);

        return byName.Values.OrderBy(a => a.IsFront ? 0 : 1).ThenBy(a => a.Name).ToList();
    }

    /// <summary>The browser to use for a web task: the one in front, else the first running one.</summary>
    public static GuideOpenApp? Browser(IReadOnlyList<GuideOpenApp> apps) => GuideBrowsers.Pick(apps);

    private static (string Name, string Process) AppNameOf(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            var description = FileDescription(process);
            var name = description.Length > 0 ? description : process.ProcessName;
            return (name, process.ProcessName);
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }

    /// <summary>"Google Chrome" rather than "chrome": the taskbar label the user actually sees.</summary>
    private static string FileDescription(Process process)
    {
        try { return process.MainModule?.FileVersionInfo.FileDescription ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static string TitleOf(IntPtr hwnd)
    {
        var length = GuideNative.GetWindowTextLengthW(hwnd);
        if (length <= 0) return string.Empty;
        var buffer = new StringBuilder(length + 1);
        GuideNative.GetWindowTextW(hwnd, buffer, buffer.Capacity);
        return buffer.ToString().Trim();
    }

    private static string ClassOf(IntPtr hwnd)
    {
        var buffer = new StringBuilder(128);
        GuideNative.GetClassNameW(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }
}
