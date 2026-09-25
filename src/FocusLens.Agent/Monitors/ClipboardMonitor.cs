using FocusLens.Agent.Stores;
using FocusLens.Core.Settings;
using FocusLens.Platform.Windows.Capture;

namespace FocusLens.Agent.Monitors;

/// <summary>Counts copy actions by app. The copied content is never read.</summary>
public sealed class ClipboardMonitor : IDisposable
{
    private readonly ClipboardWatcher _watcher = new();
    private readonly SignalStore _store;
    private readonly PrivacyFilter _privacy;
    private readonly TrackingSettingsProvider _tracking;
    private readonly PauseGate _pause;
    private readonly ForegroundCapture _foreground;
    private readonly Timer _timer;

    public ClipboardMonitor(
        SignalStore store, PrivacyFilter privacy, TrackingSettingsProvider tracking,
        PauseGate pause, ForegroundCapture foreground)
    {
        _store = store;
        _privacy = privacy;
        _tracking = tracking;
        _pause = pause;
        _foreground = foreground;
        _timer = new Timer(_ => Poll(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start() => _timer.Change(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250));

    private void Poll()
    {
        try
        {
            if (!_watcher.PollForCopy()) return;
            if (!_tracking.IsOn(TrackingItem.ClipboardActivity) || _pause.IsPaused) return;

            var context = _foreground.CaptureCurrentContext();
            if (context is null || _privacy.IsExcluded(context.AppId, null, null)) return;
            _store.InsertSystemEvent("clipboard_copy", context.AppId, context.AppName);
        }
        catch
        {
            // Skip this poll.
        }
    }

    public void Dispose() => _timer.Dispose();
}
