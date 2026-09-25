using FocusLens.Agent.Stores;
using FocusLens.Core.Settings;
using FocusLens.Platform.Windows.Capture;

namespace FocusLens.Agent.Monitors;

/// <summary>
/// Every five seconds (and on each app switch) records how many keys, clicks and scrolls
/// happened, attributed to the app that was focused during that window. Counts only.
/// </summary>
public sealed class InputMonitor : IDisposable
{
    private readonly InputCounter _counter = new();
    private readonly SignalStore _store;
    private readonly PrivacyFilter _privacy;
    private readonly TrackingSettingsProvider _tracking;
    private readonly PauseGate _pause;
    private readonly ForegroundCapture _foreground;
    private readonly object _gate = new();
    private readonly Timer _timer;

    private InputCounts _last;
    private (string AppId, string Name)? _owner;

    public InputMonitor(
        SignalStore store, PrivacyFilter privacy, TrackingSettingsProvider tracking,
        PauseGate pause, ForegroundCapture foreground)
    {
        _store = store;
        _privacy = privacy;
        _tracking = tracking;
        _pause = pause;
        _foreground = foreground;
        _timer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        _counter.Start();
        lock (_gate)
        {
            _last = _counter.Snapshot();
            _owner = Current();
        }
        _timer.Change(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    /// <summary>Call when the focused app changes so counts land on the right app.</summary>
    public void AppChanged() => Flush();

    private void Flush()
    {
        lock (_gate)
        {
            var now = _counter.Snapshot();
            var before = _last;
            var owner = _owner;
            _last = now;
            _owner = Current();

            if (!_tracking.IsOn(TrackingItem.InputActivity) || owner is null || _pause.IsPaused) return;
            if (_privacy.IsExcluded(owner.Value.AppId, null, null)) return;

            var keys = (int)unchecked(now.Keys - before.Keys);
            var clicks = (int)unchecked(now.Clicks - before.Clicks);
            var scrolls = (int)unchecked(now.Scrolls - before.Scrolls);
            if (keys + clicks + scrolls <= 0) return;
            _store.InsertInput(owner.Value.AppId, owner.Value.Name, keys, clicks, scrolls);
        }
    }

    private (string AppId, string Name)? Current()
    {
        var context = _foreground.CaptureCurrentContext();
        return context is null ? null : (context.AppId, context.AppName);
    }

    public void Dispose()
    {
        _timer.Dispose();
        _counter.Dispose();
    }
}
