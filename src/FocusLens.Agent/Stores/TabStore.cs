using FocusLens.Core.Data;
using FocusLens.Core.Logging;
using FocusLens.Core.Models;

namespace FocusLens.Agent.Stores;

/// <summary>Stores open-tab snapshots, skipping a snapshot identical to the previous one.</summary>
public sealed class TabStore
{
    private readonly Db _db;
    private readonly FileLog _log;
    private readonly object _gate = new();
    private string _lastFingerprint = "";

    public TabStore(Db db, FileLog log)
    {
        _db = db;
        _log = log;
    }

    public void Save(IReadOnlyList<BrowserTab> tabs)
    {
        if (tabs.Count == 0) return;
        var fingerprint = string.Join("\n", tabs.Select(t => t.Browser + "|" + t.Title + "|" + t.Url).OrderBy(x => x));
        lock (_gate)
        {
            if (fingerprint == _lastFingerprint) return;
            _lastFingerprint = fingerprint;
        }

        try
        {
            var now = DateTime.UtcNow;
            _db.InTransaction((connection, transaction) => Dapper.SqlMapper.Execute(connection,
                "INSERT INTO open_tabs (timestamp, browser, title, url) VALUES (@now, @Browser, @Title, @Url)",
                tabs.Select(t => new { now, t.Browser, t.Title, t.Url }), transaction));
        }
        catch (Exception ex)
        {
            _log.Error("Tab snapshot insert failed", ex);
        }
    }
}
