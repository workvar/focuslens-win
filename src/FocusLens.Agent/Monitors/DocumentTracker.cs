using System.Text.RegularExpressions;
using FocusLens.Agent.Stores;
using FocusLens.Core.Settings;
using FocusLens.Platform.Windows.Capture;

namespace FocusLens.Agent.Monitors;

/// <summary>
/// Notes the file open in the focused window. Windows has no universal "document path" property,
/// so this recognises absolute paths that editors and viewers show in their window titles.
/// </summary>
public sealed class DocumentTracker
{
    private static readonly Regex PathPattern =
        new(@"(?<path>[A-Za-z]:\\(?:[^\\/:*?""<>|\r\n]+\\)*[^\\/:*?""<>|\r\n]+\.[A-Za-z0-9]{1,8})", RegexOptions.Compiled);

    private readonly SignalStore _store;
    private readonly PrivacyFilter _privacy;
    private readonly TrackingSettingsProvider _tracking;
    private readonly Dictionary<string, string> _lastPath = new();
    private DateTime _lastCheck = DateTime.MinValue;

    public DocumentTracker(SignalStore store, PrivacyFilter privacy, TrackingSettingsProvider tracking)
    {
        _store = store;
        _privacy = privacy;
        _tracking = tracking;
    }

    public void Observe(string appId, string appName, string? windowTitle)
    {
        if (!_tracking.IsOn(TrackingItem.DocumentPaths) || BrowserCatalog.IsBrowser(appId)) return;
        if (string.IsNullOrEmpty(windowTitle) || DateTime.UtcNow - _lastCheck < TimeSpan.FromSeconds(2)) return;
        _lastCheck = DateTime.UtcNow;

        var match = PathPattern.Match(windowTitle);
        if (!match.Success) return;

        var path = match.Groups["path"].Value;
        if (_lastPath.TryGetValue(appId, out var previous) && previous == path) return;
        if (_privacy.IsExcluded(appId, null, path)) return;

        _lastPath[appId] = path;
        _store.InsertDocument(appId, appName, path);
    }
}
