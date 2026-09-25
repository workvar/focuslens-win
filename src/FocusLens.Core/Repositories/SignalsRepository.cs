using FocusLens.Core.Context;
using FocusLens.Core.Data;

namespace FocusLens.Core.Repositories;

/// <summary>Reads activity signals (input counts, files, copies, system events) for a range.</summary>
public sealed class SignalsRepository
{
    private readonly Db _db;

    public SignalsRepository(Db db) => _db = db;

    public Task<ActivitySignals> SignalsAsync(DateTime from, DateTime to) => Task.Run(() =>
    {
        var result = new ActivitySignals();

        result.Input.AddRange(_db.Query<InputRow>(@"
            SELECT app_name AS AppName, SUM(keys) AS K, SUM(clicks) AS C, SUM(scrolls) AS S
            FROM input_activity
            WHERE timestamp >= @from AND timestamp <= @to
            GROUP BY app_bundle_id, app_name
            ORDER BY SUM(keys) + SUM(clicks) DESC
            LIMIT 8", new { from, to })
            .Select(r => new InputTotal(r.AppName, (int)r.K, (int)r.C, (int)r.S)));

        result.Documents.AddRange(_db.Query<DocRow>(@"
            SELECT app_name AS AppName, path AS Path, MAX(timestamp) AS Seen
            FROM document_log
            WHERE timestamp >= @from AND timestamp <= @to
            GROUP BY path
            ORDER BY Seen DESC
            LIMIT 12", new { from, to })
            .Select(r => new DocumentVisit(r.AppName, r.Path, r.Seen)));

        var events = _db.Query<EventRow>(@"
            SELECT timestamp AS Timestamp, kind AS Kind, detail AS Detail
            FROM system_events
            WHERE timestamp >= @from AND timestamp <= @to
            ORDER BY timestamp DESC
            LIMIT 200", new { from, to });
        foreach (var row in events)
        {
            if (row.Kind == "clipboard_copy")
            {
                var key = row.Detail ?? "unknown";
                result.CopiesByApp[key] = result.CopiesByApp.GetValueOrDefault(key) + 1;
            }
            else
            {
                result.Events.Add(new SystemEventRecord(row.Timestamp, row.Kind, row.Detail));
            }
        }
        return result;
    });

    private sealed class InputRow
    {
        public string AppName { get; set; } = "";
        public long K { get; set; }
        public long C { get; set; }
        public long S { get; set; }
    }

    private sealed class DocRow
    {
        public string AppName { get; set; } = "";
        public string Path { get; set; } = "";
        public DateTime Seen { get; set; }
    }

    private sealed class EventRow
    {
        public DateTime Timestamp { get; set; }
        public string Kind { get; set; } = "";
        public string? Detail { get; set; }
    }
}
