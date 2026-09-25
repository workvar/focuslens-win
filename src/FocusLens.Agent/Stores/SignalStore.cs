using FocusLens.Core.Data;
using FocusLens.Core.Logging;

namespace FocusLens.Agent.Stores;

/// <summary>Writes activity signals (counts and events only) and prunes them after 14 days.</summary>
public sealed class SignalStore
{
    private const int RetentionDays = 14;

    private readonly Db _db;
    private readonly FileLog _log;

    public SignalStore(Db db, FileLog log)
    {
        _db = db;
        _log = log;
    }

    public void InsertInput(string appId, string appName, int keys, int clicks, int scrolls) =>
        Run(@"INSERT INTO input_activity (timestamp, app_bundle_id, app_name, keys, clicks, scrolls)
              VALUES (@now, @appId, @appName, @keys, @clicks, @scrolls)",
            new { now = DateTime.UtcNow, appId, appName, keys, clicks, scrolls });

    public void InsertSystemEvent(string kind, string? appId = null, string? detail = null) =>
        Run("INSERT INTO system_events (timestamp, kind, app_bundle_id, detail) VALUES (@now, @kind, @appId, @detail)",
            new { now = DateTime.UtcNow, kind, appId, detail });

    public void InsertDocument(string appId, string appName, string path) =>
        Run("INSERT INTO document_log (timestamp, app_bundle_id, app_name, path) VALUES (@now, @appId, @appName, @path)",
            new { now = DateTime.UtcNow, appId, appName, path });

    public void PruneOld()
    {
        var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
        foreach (var table in new[] { "input_activity", "system_events", "document_log", "open_tabs" })
            Run($"DELETE FROM {table} WHERE timestamp < @cutoff", new { cutoff });
    }

    private void Run(string sql, object args)
    {
        try { _db.Execute(sql, args); }
        catch (Exception ex) { _log.Error("Signal write failed", ex); }
    }
}
