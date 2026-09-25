using FocusLens.Agent.Stores;
using FocusLens.Core.Settings;
using FocusLens.Platform.Windows.Native;
using FocusLens.Platform.Windows.Shell;
using Microsoft.Win32;

namespace FocusLens.Agent.Monitors;

/// <summary>Lock, unlock, sleep and wake events, plus app launches and quits (by watching the process list).</summary>
public sealed class SystemEventMonitor : IDisposable
{
    private readonly SignalStore _store;
    private readonly PrivacyFilter _privacy;
    private readonly TrackingSettingsProvider _tracking;
    private readonly Timer _timer;
    private HashSet<string> _running = new();
    private bool _primed;

    public SystemEventMonitor(SignalStore store, PrivacyFilter privacy, TrackingSettingsProvider tracking)
    {
        _store = store;
        _privacy = privacy;
        _tracking = tracking;
        _timer = new Timer(_ => PollApps(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        _timer.Change(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (!_tracking.IsOn(TrackingItem.ScreenState)) return;
        var kind = e.Reason switch
        {
            SessionSwitchReason.SessionLock => "session_inactive",
            SessionSwitchReason.SessionUnlock => "session_active",
            _ => null,
        };
        if (kind is not null) _store.InsertSystemEvent(kind);
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (!_tracking.IsOn(TrackingItem.ScreenState)) return;
        var kind = e.Mode switch
        {
            PowerModes.Suspend => "system_sleep",
            PowerModes.Resume => "system_wake",
            _ => null,
        };
        if (kind is not null) _store.InsertSystemEvent(kind);
    }

    /// <summary>Diffing the set of processes that own a visible window approximates app launch and quit.</summary>
    private void PollApps()
    {
        try
        {
            var current = new HashSet<string>(
                FocusLens.Platform.Windows.Capture.WindowInventory.VisibleWindows((uint)Environment.ProcessId)
                    .Select(w => w.AppId));

            if (_primed && _tracking.IsOn(TrackingItem.AppLaunches))
            {
                foreach (var appId in current.Except(_running)) Record("app_launch", appId);
                foreach (var appId in _running.Except(current)) Record("app_quit", appId);
            }
            _running = current;
            _primed = true;
        }
        catch
        {
            // A failed poll just skips this round.
        }
    }

    private void Record(string kind, string appId)
    {
        if (_privacy.IsExcluded(appId, null, null)) return;
        _store.InsertSystemEvent(kind, appId, appId);
    }

    public void Dispose()
    {
        _timer.Dispose();
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }
}
