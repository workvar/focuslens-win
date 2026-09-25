using FocusLens.Core.Settings;

namespace FocusLens.Agent;

/// <summary>Cached read of the user's "pause tracking" switch, which the app writes to shared state.</summary>
public sealed class PauseGate
{
    private readonly object _gate = new();
    private bool _paused;
    private DateTime _checkedAt = DateTime.MinValue;

    public bool IsPaused
    {
        get
        {
            lock (_gate)
            {
                if (DateTime.UtcNow - _checkedAt > TimeSpan.FromSeconds(2))
                {
                    _paused = SharedState.IsTrackingPaused();
                    _checkedAt = DateTime.UtcNow;
                }
                return _paused;
            }
        }
    }
}
