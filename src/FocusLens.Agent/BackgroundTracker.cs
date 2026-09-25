using FocusLens.Agent.Stores;
using FocusLens.Core.Logging;
using FocusLens.Core.Models;
using FocusLens.Core.Settings;
using FocusLens.Platform.Windows.Capture;

namespace FocusLens.Agent;

/// <summary>
/// Reads windows the user is not focused on, one at a time in round-robin (about every 30 s), and
/// snapshots open browser tabs. Background text is tagged so it never counts as focused time.
/// </summary>
public sealed class BackgroundTracker : IDisposable
{
    private const int TickSeconds = 2;
    private const int GuardEveryTicks = 15;   // round robin every 30 s
    private const int TabsEveryTicks = 15;    // tab list at least every 30 s
    private const int MinTextLength = 40;

    private readonly ScreenTextReader _reader;
    private readonly ScreenshotStore _store;
    private readonly TabStore _tabStore;
    private readonly PrivacyFilter _privacy;
    private readonly IdleDetector _idle;
    private readonly TrackingSettingsProvider _tracking;
    private readonly PauseGate _pause;
    private readonly FileLog _log;
    private readonly Timer _timer;

    private int _ticks;
    private int _cursor;
    private bool _inFlight;

    public BackgroundTracker(
        ScreenTextReader reader, ScreenshotStore store, TabStore tabStore, PrivacyFilter privacy,
        IdleDetector idle, TrackingSettingsProvider tracking, PauseGate pause, FileLog log)
    {
        _reader = reader;
        _store = store;
        _tabStore = tabStore;
        _privacy = privacy;
        _idle = idle;
        _tracking = tracking;
        _pause = pause;
        _log = log;
        _timer = new Timer(_ => Tick(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start() => _timer.Change(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(TickSeconds));

    private void Tick()
    {
        try
        {
            if (_pause.IsPaused || _idle.TimeSinceLastInput() >= TimeSpan.FromSeconds(60)) return;
            _ticks++;

            if (_tracking.IsOn(TrackingItem.BrowserTabs) && _ticks % TabsEveryTicks == 1) ReadTabs();
            if (_tracking.IsOn(TrackingItem.BackgroundWindowText) && !_inFlight && _ticks % GuardEveryTicks == 0)
                ReadNextWindow();
        }
        catch (Exception ex)
        {
            _log.Error("Background tick failed", ex);
        }
    }

    private void ReadTabs()
    {
        var allowed = BrowserTabsReader.ReadAll((uint)Environment.ProcessId)
            .Where(t => !_privacy.IsExcluded(t.AppId, string.IsNullOrEmpty(t.Url) ? null : t.Url, t.Title))
            .ToList();
        _tabStore.Save(allowed);
    }

    private void ReadNextWindow()
    {
        var foregroundPid = new ForegroundCapture().ForegroundPid();
        var windows = WindowInventory.VisibleWindows((uint)Environment.ProcessId)
            .Where(w => w.ProcessId != foregroundPid).ToList();
        if (windows.Count == 0) return;

        _cursor = (_cursor + 1) % windows.Count;
        var window = windows[_cursor];
        if (_privacy.IsExcluded(window.AppId, null, window.Title)) return;

        _inFlight = true;
        _ = Task.Run(async () =>
        {
            try
            {
                var text = await _reader.ReadAsync(window.Handle, window.AppId, allowOcr: true);
                if (text is { Length: >= MinTextLength } && !_privacy.OcrIsSensitive(text))
                {
                    var title = _tracking.IsOn(TrackingItem.WindowTitles) ? window.Title : null;
                    _store.Save(DateTime.UtcNow, window.AppId, window.AppName, title, text, "background");
                }
            }
            catch (Exception ex)
            {
                _log.Error("Background window read failed", ex);
            }
            finally
            {
                _inFlight = false;
            }
        });
    }

    public void Dispose() => _timer.Dispose();
}
