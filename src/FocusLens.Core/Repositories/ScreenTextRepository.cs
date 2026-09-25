using FocusLens.Core.Context;
using FocusLens.Core.Data;

namespace FocusLens.Core.Repositories;

/// <summary>Reads on-screen text snapshots, page URLs and open tabs for the AI context.</summary>
public sealed class ScreenTextRepository
{
    private readonly Db _db;

    public ScreenTextRepository(Db db) => _db = db;

    public Task<IReadOnlyList<ScreenTextSnapshot>> SnapshotsAsync(DateTime from, DateTime to, int limit = 1500) =>
        Task.Run<IReadOnlyList<ScreenTextSnapshot>>(() => _db.Query<SnapshotRow>(@"
            SELECT timestamp AS Timestamp, app_bundle_id AS AppId, app_name AS AppName,
                   window_title AS Title, ocr_text AS Text, focus_state AS FocusState
            FROM screenshots
            WHERE timestamp >= @from AND timestamp <= @to AND ocr_text IS NOT NULL AND ocr_text != ''
            ORDER BY timestamp ASC
            LIMIT @limit", new { from, to, limit })
            .Select(r => new ScreenTextSnapshot(r.Timestamp, r.AppId, r.AppName, r.Title, r.Text, r.FocusState == "background"))
            .ToList());

    /// <summary>Most common URL per (app, window title), keyed for SessionBuilder.</summary>
    public Task<Dictionary<string, string>> UrlsAsync(DateTime from, DateTime to) =>
        Task.Run(() =>
        {
            var map = new Dictionary<string, string>();
            var rows = _db.Query<UrlRow>(@"
                SELECT app_bundle_id AS AppId, window_title AS Title, url AS Url, COUNT(*) AS C
                FROM activity_events
                WHERE timestamp >= @from AND timestamp <= @to AND url IS NOT NULL AND url != ''
                GROUP BY app_bundle_id, window_title, url
                ORDER BY C DESC", new { from, to });
            foreach (var row in rows)
                map.TryAdd(SessionBuilder.Key(row.AppId, row.Title), row.Url);
            return map;
        });

    public Task<IReadOnlyList<OpenTab>> OpenTabsAsync(DateTime from, DateTime to, int limit = 200) =>
        Task.Run<IReadOnlyList<OpenTab>>(() => _db.Query<TabRow>(@"
            SELECT browser AS Browser, title AS Title, url AS Url, MAX(timestamp) AS Seen
            FROM open_tabs
            WHERE timestamp >= @from AND timestamp <= @to
            GROUP BY url, title
            ORDER BY Seen DESC
            LIMIT @limit", new { from, to, limit })
            .Select(r => new OpenTab(r.Browser, r.Title ?? "", r.Url, r.Seen)).ToList());

    private sealed class SnapshotRow
    {
        public DateTime Timestamp { get; set; }
        public string AppId { get; set; } = "";
        public string AppName { get; set; } = "";
        public string? Title { get; set; }
        public string Text { get; set; } = "";
        public string FocusState { get; set; } = "foreground";
    }

    private sealed class UrlRow
    {
        public string AppId { get; set; } = "";
        public string? Title { get; set; }
        public string Url { get; set; } = "";
    }

    private sealed class TabRow
    {
        public string Browser { get; set; } = "";
        public string? Title { get; set; }
        public string Url { get; set; } = "";
        public DateTime Seen { get; set; }
    }
}
