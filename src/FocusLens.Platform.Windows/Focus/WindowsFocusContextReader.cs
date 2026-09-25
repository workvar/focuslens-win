using FocusLens.Core.Focus;
using FocusLens.Core.Focus.Session;
using FocusLens.Platform.Windows.Capture;
using FocusLens.Platform.Windows.Native;
using FocusLens.Platform.Windows.Shell;

namespace FocusLens.Platform.Windows.Focus;

/// <summary>
/// Reads the foreground app, its window title and, for browsers, the URL of the current tab.
/// The title and position are plain Win32 reads and never block. The URL comes from UI
/// Automation, which waits on the browser, so it runs off the calling thread with a timeout
/// (see <see cref="FocusUrlProbe"/>). A read that times out keeps the last good reading: a
/// browser busy playing a video must not look like "a browser with no page".
/// </summary>
public sealed class WindowsFocusContextReader : IFocusContextReader
{
    /// <summary>Windows' own surfaces: never a distraction, whatever the goal.</summary>
    private static readonly HashSet<string> IgnoredApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe", "searchhost.exe", "searchapp.exe", "startmenuexperiencehost.exe",
        "shellexperiencehost.exe", "lockapp.exe", "textinputhost.exe", "systemsettings.exe",
        "logonui.exe", "focuslensagent.exe",
    };

    /// <summary>How long a busy app keeps its last reading before it counts as unknown.</summary>
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(10);

    private readonly uint _ownPid = (uint)Environment.ProcessId;
    private readonly FocusUrlProbe _urls = new();
    private readonly Dictionary<string, string> _appNames = new();
    private (FocusContext Context, DateTime At)? _lastGood;

    public async Task<FocusContext?> CurrentAsync(bool allowStale = true)
    {
        var hwnd = User32.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;
        User32.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0 || pid == _ownPid) return null;

        var appId = ProcessInfo.AppIdOf(pid);
        if (appId is null || IgnoredApps.Contains(appId)) return null;

        var isBrowser = BrowserCatalog.IsBrowser(appId);
        var title = User32.GetTitle(hwnd);
        var bounds = User32.GetWindowRect(hwnd, out var r) ? new FocusRect(r.Left, r.Top, r.Width, r.Height) : (FocusRect?)null;

        string? url = null;
        if (isBrowser)
        {
            var probe = await _urls.ReadAsync(hwnd, pid, title);
            if (probe.TimedOut && allowStale && _lastGood is { } last && last.Context.Pid == pid
                && last.Context.WindowTitle == title && DateTime.UtcNow - last.At < StaleAfter)
            {
                return last.Context.WithBounds(bounds);
            }
            url = probe.Url;
        }

        var context = new FocusContext
        {
            AppName = AppName(pid, appId, isBrowser),
            AppId = appId,
            Pid = pid,
            Window = hwnd.ToInt64(),
            WindowTitle = title,
            Url = url,
            IsBrowser = isBrowser,
            Bounds = bounds,
        };
        if (context.IsJudgeable) _lastGood = (context, DateTime.UtcNow);
        return context;
    }

    /// <summary>The friendly name reads the exe's version info, so it is cached per app.</summary>
    private string AppName(uint pid, string appId, bool isBrowser)
    {
        if (_appNames.TryGetValue(appId, out var name)) return name;
        name = isBrowser ? BrowserCatalog.DisplayName(appId) : ProcessInfo.AppNameOf(pid, appId);
        _appNames[appId] = name;
        return name;
    }
}
