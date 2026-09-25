using Dapper;
using FocusLens.Core.Context;

namespace FocusLens.Core.Repositories;

public sealed partial class ScreenTextRepository
{
    /// <summary>
    /// Snapshots whose text, window title or app name contains any of the terms, newest first
    /// in the query and oldest first in the result. Used so a specific question finds its
    /// data even when it is older than the newest snapshots.
    /// </summary>
    public Task<IReadOnlyList<ScreenTextSnapshot>> SearchSnapshotsAsync(
        DateTime from, DateTime to, IReadOnlyList<string> terms, int limit = 600)
    {
        if (terms.Count == 0) return Task.FromResult<IReadOnlyList<ScreenTextSnapshot>>(Array.Empty<ScreenTextSnapshot>());

        return Task.Run<IReadOnlyList<ScreenTextSnapshot>>(() =>
        {
            var parameters = new DynamicParameters();
            parameters.Add("from", from);
            parameters.Add("to", to);
            parameters.Add("limit", limit);

            var clauses = new List<string>();
            for (var i = 0; i < terms.Count; i++)
            {
                parameters.Add($"t{i}", "%" + terms[i] + "%");
                clauses.Add($"(ocr_text LIKE @t{i} OR window_title LIKE @t{i} OR app_name LIKE @t{i})");
            }

            var sql = $@"
                SELECT timestamp AS Timestamp, app_bundle_id AS AppId, app_name AS AppName,
                       window_title AS Title, ocr_text AS Text, focus_state AS FocusState
                FROM screenshots
                WHERE timestamp >= @from AND timestamp <= @to AND ocr_text IS NOT NULL AND ocr_text != ''
                  AND ({string.Join(" OR ", clauses)})
                ORDER BY timestamp DESC
                LIMIT @limit";

            using var connection = _db.Open();
            return ToSnapshots(connection.Query<SnapshotRow>(sql, parameters)).OrderBy(s => s.Timestamp).ToList();
        });
    }

    /// <summary>How many text snapshots exist per app in the window, most captured first.</summary>
    public Task<IReadOnlyList<(string App, int Count)>> CoverageAsync(DateTime from, DateTime to, int top = 12) =>
        Task.Run<IReadOnlyList<(string App, int Count)>>(() => _db.Query<CoverageRow>(@"
            SELECT app_name AS App, COUNT(*) AS Count
            FROM screenshots
            WHERE timestamp >= @from AND timestamp <= @to AND ocr_text IS NOT NULL AND ocr_text != ''
            GROUP BY app_name
            ORDER BY Count DESC
            LIMIT @top", new { from, to, top }).Select(r => (r.App, r.Count)).ToList());

    private sealed class CoverageRow
    {
        public string App { get; set; } = "";
        public int Count { get; set; }
    }
}
