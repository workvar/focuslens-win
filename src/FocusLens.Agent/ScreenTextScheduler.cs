using FocusLens.Agent.Stores;
using FocusLens.Core.Logging;
using FocusLens.Core.Models;
using FocusLens.Core.Settings;

namespace FocusLens.Agent;

/// <summary>
/// Decides when to read the focused window's text: when the window changes, and at least every
/// 20 seconds while the user is active. Never runs while idle, paused, or on an excluded app.
/// </summary>
public sealed class ScreenTextScheduler
{
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PeriodicInterval = TimeSpan.FromSeconds(20);

    private readonly ScreenTextReader _reader;
    private readonly ScreenshotStore _store;
    private readonly PrivacyFilter _privacy;
    private readonly TrackingSettingsProvider _tracking;
    private readonly PauseGate _pause;
    private readonly FileLog _log;
    private readonly object _gate = new();

    private string _lastKey = "";
    private DateTime _lastTime = DateTime.MinValue;
    private bool _inFlight;

    public ScreenTextScheduler(
        ScreenTextReader reader, ScreenshotStore store, PrivacyFilter privacy,
        TrackingSettingsProvider tracking, PauseGate pause, FileLog log)
    {
        _reader = reader;
        _store = store;
        _privacy = privacy;
        _tracking = tracking;
        _pause = pause;
        _log = log;
    }

    public void Consider(CaptureContext context, IntPtr hwnd, bool isIdle)
    {
        if (!_tracking.IsOn(TrackingItem.FocusedScreenText) || isIdle || _pause.IsPaused) return;
        if (_privacy.IsExcluded(context.AppId, context.Url, context.WindowTitle)) return;

        var key = context.AppId + "|" + (context.WindowTitle ?? "");
        var now = DateTime.UtcNow;
        lock (_gate)
        {
            if (_inFlight) return;
            var sinceLast = now - _lastTime;
            var changed = key != _lastKey;
            if (!((changed && sinceLast >= MinInterval) || sinceLast >= PeriodicInterval)) return;

            _inFlight = true;
            _lastKey = key;
            _lastTime = now;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var text = await _reader.ReadAsync(hwnd, context.AppId, allowOcr: true);
                if (text is not null && !_privacy.OcrIsSensitive(text))
                {
                    var title = _tracking.IsOn(TrackingItem.WindowTitles) ? context.WindowTitle : null;
                    _store.Save(now, context.AppId, context.AppName, title, text);
                }
            }
            catch (Exception ex)
            {
                _log.Error("Focused text read failed", ex);
            }
            finally
            {
                lock (_gate) _inFlight = false;
            }
        });
    }
}
