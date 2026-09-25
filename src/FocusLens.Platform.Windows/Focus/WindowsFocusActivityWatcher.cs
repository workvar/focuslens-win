using FocusLens.Core.Focus.Session;
using FocusLens.Platform.Windows.Capture;
using FocusLens.Platform.Windows.Native;
using Microsoft.Win32;

namespace FocusLens.Platform.Windows.Focus;

/// <summary>
/// Tells the focus controller when to look, so it can look less often:
///
///   window switch  reported at once (a foreground WinEvent hook), so the poll interval only
///                  has to catch tab and page changes inside an app
///   locked         or suspended, or switched to another user: no reads, and the bar does not drain
///   idle           no keyboard or mouse input for a while: nothing on screen is changing
///
/// Everything here is a system notification or a single cheap query; nothing polls.
/// Start must be called on a thread with a message loop (the UI thread): the hook delivers there.
/// </summary>
public sealed class WindowsFocusActivityWatcher : IFocusActivityWatcher
{
    private readonly IdleDetector _idle = new();
    // Kept in a field: the hook calls this delegate from native code, so it must not be collected.
    private readonly User32.WinEventProc _onForeground;
    private IntPtr _hook;
    private Action? _onAppSwitch;
    private SynchronizationContext? _context;
    private volatile bool _asleep;

    public WindowsFocusActivityWatcher() => _onForeground = (_, _, _, _, _, _, _) => _onAppSwitch?.Invoke();

    public bool IsScreenAsleep => _asleep;

    public void Start(Action onAppSwitch)
    {
        Stop();
        _onAppSwitch = onAppSwitch;
        _context = SynchronizationContext.Current;
        _hook = User32.SetWinEventHook(
            User32.EVENT_SYSTEM_FOREGROUND, User32.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _onForeground,
            0, 0, User32.WINEVENT_OUTOFCONTEXT | User32.WINEVENT_SKIPOWNPROCESS);
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public void Stop()
    {
        if (_hook != IntPtr.Zero) User32.UnhookWinEvent(_hook);
        _hook = IntPtr.Zero;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _onAppSwitch = null;
        _asleep = false;
    }

    public double IdleSeconds() => _idle.TimeSinceLastInput().TotalSeconds;

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
            case SessionSwitchReason.ConsoleDisconnect:
            case SessionSwitchReason.RemoteDisconnect:
                _asleep = true;
                break;
            case SessionSwitchReason.SessionUnlock:
            case SessionSwitchReason.ConsoleConnect:
            case SessionSwitchReason.RemoteConnect:
                Wake();
                break;
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend) _asleep = true;
        else if (e.Mode == PowerModes.Resume) Wake();
    }

    /// <summary>Back at the desk: look at the screen right away. SystemEvents fire on their own thread.</summary>
    private void Wake()
    {
        _asleep = false;
        var callback = _onAppSwitch;
        if (callback is null) return;
        if (_context is { } context) context.Post(_ => callback(), null);
        else callback();
    }
}
