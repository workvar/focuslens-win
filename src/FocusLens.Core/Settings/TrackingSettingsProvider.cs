namespace FocusLens.Core.Settings;

/// <summary>Cached view of tracking.json for hot loops; reloads every few seconds.</summary>
public sealed class TrackingSettingsProvider
{
    private readonly object _gate = new();
    private readonly TimeSpan _refresh = TimeSpan.FromSeconds(3);
    private TrackingSettings _settings = TrackingSettings.Load();
    private DateTime _loadedAt = DateTime.UtcNow;

    public bool IsOn(TrackingItem item)
    {
        lock (_gate)
        {
            if (DateTime.UtcNow - _loadedAt > _refresh)
            {
                _settings = TrackingSettings.Load();
                _loadedAt = DateTime.UtcNow;
            }
            return _settings.IsOn(item);
        }
    }
}
