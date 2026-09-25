using FocusLens.Core.Privacy;

namespace FocusLens.Agent;

/// <summary>Cached view of privacy.json; reloads every 30 seconds so UI changes take effect quickly.</summary>
public sealed class PrivacyFilter
{
    private static readonly TimeSpan Refresh = TimeSpan.FromSeconds(30);
    private readonly object _gate = new();
    private PrivacyConfig _config = PrivacyConfig.Load();
    private DateTime _loadedAt = DateTime.UtcNow;

    private PrivacyConfig Current()
    {
        lock (_gate)
        {
            if (DateTime.UtcNow - _loadedAt > Refresh)
            {
                _config = PrivacyConfig.Load();
                _loadedAt = DateTime.UtcNow;
            }
            return _config;
        }
    }

    public string? RestrictingCategory(string appId, string? url, string? windowTitle) =>
        Current().RestrictingCategory(appId, url, windowTitle);

    public bool IsExcluded(string appId, string? url, string? windowTitle) =>
        RestrictingCategory(appId, url, windowTitle) is not null;

    public bool OcrIsSensitive(string text) => Current().OcrIsSensitive(text);
}
