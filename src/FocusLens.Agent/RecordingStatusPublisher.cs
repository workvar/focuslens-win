using FocusLens.Core.Settings;

namespace FocusLens.Agent;

/// <summary>Tells the app when recording is suppressed by a privacy category (only on change).</summary>
public sealed class RecordingStatusPublisher
{
    private readonly object _gate = new();
    private bool? _lastRestricted;
    private string? _lastReason;

    public void Publish(bool restricted, string? reason, string appName)
    {
        lock (_gate)
        {
            if (restricted == _lastRestricted && reason == _lastReason) return;
            _lastRestricted = restricted;
            _lastReason = reason;
        }

        SharedState.Update(state =>
        {
            state.RecordingRestricted = restricted;
            state.RecordingRestrictedReason = reason ?? "";
            state.RecordingRestrictedApp = appName;
            state.RecordingStatusAt = SharedState.NowUnix();
        });
    }
}
