using FocusLens.Core.Models;
using FocusLens.Platform.Windows.Native;
using FocusLens.Platform.Windows.Shell;

namespace FocusLens.Platform.Windows.Capture;

/// <summary>Reads what the user is focused on right now: app, window title, and browser URL.</summary>
public sealed class ForegroundCapture
{
    private readonly BrowserUrlReader _urlReader = new();
    private readonly uint _ownPid = (uint)Environment.ProcessId;

    public CaptureContext? CaptureCurrentContext()
    {
        var hwnd = User32.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;

        User32.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0 || pid == _ownPid) return null;

        var appId = ProcessInfo.AppIdOf(pid);
        if (appId is null) return null;

        var title = User32.GetTitle(hwnd);
        var appName = ProcessInfo.AppNameOf(pid, appId);
        var url = BrowserCatalog.IsBrowser(appId) ? _urlReader.ReadUrl(hwnd) : null;

        return new CaptureContext(appId, appName, string.IsNullOrEmpty(title) ? null : title, url);
    }

    public IntPtr ForegroundHandle() => User32.GetForegroundWindow();

    public uint? ForegroundPid()
    {
        var hwnd = User32.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;
        User32.GetWindowThreadProcessId(hwnd, out var pid);
        return pid == 0 ? null : pid;
    }
}
