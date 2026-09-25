using FocusLens.Core.Data;
using FocusLens.Core.Logging;

namespace FocusLens.Agent.Stores;

/// <summary>Stores on-screen text (never images). Skips unchanged text and prunes after 14 days.</summary>
public sealed class ScreenshotStore
{
    private const int RetentionDays = 14;

    private readonly Db _db;
    private readonly FileLog _log;
    private readonly object _gate = new();
    private readonly Dictionary<string, string> _lastTextByWindow = new();

    public ScreenshotStore(Db db, FileLog log)
    {
        _db = db;
        _log = log;
    }

    public void Save(DateTime timestamp, string appId, string appName, string? windowTitle, string? ocrText, string focusState = "foreground")
    {
        if (string.IsNullOrEmpty(ocrText)) return;
        if (IsDuplicate(appId + "|" + (windowTitle ?? ""), ocrText)) return;

        try
        {
            _db.Execute(@"
                INSERT INTO screenshots (timestamp, app_bundle_id, app_name, window_title, ocr_text, thumb_path, focus_state, created_at)
                VALUES (@timestamp, @appId, @appName, @windowTitle, @ocrText, NULL, @focusState, @now)",
                new { timestamp, appId, appName, windowTitle, ocrText, focusState, now = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _log.Error("Screen text insert failed", ex);
        }
    }

    private bool IsDuplicate(string key, string text)
    {
        lock (_gate)
        {
            if (_lastTextByWindow.TryGetValue(key, out var last) && last == text) return true;
            _lastTextByWindow[key] = text;
            if (_lastTextByWindow.Count > 200) _lastTextByWindow.Clear();
            return false;
        }
    }

    public void PruneOld()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
            _db.Execute("DELETE FROM screenshots WHERE timestamp < @cutoff", new { cutoff });
        }
        catch (Exception ex)
        {
            _log.Error("Screen text prune failed", ex);
        }
    }
}
