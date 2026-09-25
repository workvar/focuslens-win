using FocusLens.Agent.Monitors;
using FocusLens.Agent.Stores;
using FocusLens.Core.Logging;
using FocusLens.Core.Models;
using FocusLens.Core.Settings;
using FocusLens.Platform.Windows.Capture;

namespace FocusLens.Agent;

/// <summary>The one-second heartbeat of tracking: capture context, decide idle, record, hand off to helpers.</summary>
public sealed class CaptureLoop
{
    private static readonly TimeSpan IdleThreshold = TimeSpan.FromSeconds(60);

    private readonly ForegroundCapture _foreground;
    private readonly IdleDetector _idle;
    private readonly TrackingSettingsProvider _tracking;
    private readonly PrivacyFilter _privacy;
    private readonly PauseGate _pause;
    private readonly RecordingStatusPublisher _status;
    private readonly BatchWriter _writer;
    private readonly DocumentTracker _documents;
    private readonly ScreenTextScheduler _screenText;
    private readonly InputMonitor _input;
    private readonly FileLog _log;

    private string _lastAppId = "";

    public CaptureLoop(
        ForegroundCapture foreground, IdleDetector idle, TrackingSettingsProvider tracking, PrivacyFilter privacy,
        PauseGate pause, RecordingStatusPublisher status, BatchWriter writer, DocumentTracker documents,
        ScreenTextScheduler screenText, InputMonitor input, FileLog log)
    {
        _foreground = foreground;
        _idle = idle;
        _tracking = tracking;
        _privacy = privacy;
        _pause = pause;
        _status = status;
        _writer = writer;
        _documents = documents;
        _screenText = screenText;
        _input = input;
        _log = log;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var heartbeatAt = DateTime.MinValue;

        while (await SafeWaitAsync(timer, ct))
        {
            try
            {
                Tick();
                if (DateTime.UtcNow - heartbeatAt > TimeSpan.FromSeconds(10))
                {
                    SharedState.Update(s => s.AgentHeartbeat = SharedState.NowUnix());
                    heartbeatAt = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _log.Error("Capture tick failed", ex);
            }
        }
    }

    private void Tick()
    {
        if (_pause.IsPaused) return;

        var context = _foreground.CaptureCurrentContext();
        if (context is null) return;

        if (context.AppId != _lastAppId)
        {
            _lastAppId = context.AppId;
            _input.AppChanged();
        }

        var isIdle = _tracking.IsOn(TrackingItem.IdleDetection) && _idle.IsIdle(IdleThreshold);
        var restricting = _privacy.RestrictingCategory(context.AppId, context.Url, context.WindowTitle);
        _status.Publish(restricting is not null, restricting, context.AppName);

        if (_tracking.IsOn(TrackingItem.ActiveApp) && restricting is null)
        {
            _writer.Enqueue(new ActivityEvent
            {
                Timestamp = DateTime.UtcNow,
                AppBundleId = context.AppId,
                AppName = context.AppName,
                WindowTitle = _tracking.IsOn(TrackingItem.WindowTitles) ? context.WindowTitle : null,
                Url = _tracking.IsOn(TrackingItem.BrowserUrls) ? context.Url : null,
                IsIdle = isIdle,
            });
        }

        _documents.Observe(context.AppId, context.AppName, context.WindowTitle);
        _screenText.Consider(context, _foreground.ForegroundHandle(), isIdle);
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }
}
