using System.Text.Json;
using FocusLens.Core.Categorization;
using FocusLens.Core.Data;
using FocusLens.Core.Models;
using FocusLens.Core.Storage;

namespace FocusLens.Core.Summary;

/// <summary>
/// Aggregates raw one-second activity rows into a DailySummary. Past days are
/// immutable once built; today's summary is rebuilt at most every 15 minutes.
/// </summary>
public sealed class SummaryBuilder
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

    private readonly Db _db;
    private readonly CategorizationService _categorizer;

    public SummaryBuilder(Db db, CategorizationService categorizer)
    {
        _db = db;
        _categorizer = categorizer;
    }

    public static string DateKey(DateTime date) => date.ToString("yyyy-MM-dd");

    public Task BuildIfNeededAsync(DateTime date) => Task.Run(() => BuildIfNeeded(date));

    public void BuildIfNeeded(DateTime date)
    {
        var dateKey = DateKey(date);
        var existing = _db.QueryFirstOrDefault<DailySummary>(
            "SELECT * FROM daily_summaries WHERE date = @dateKey", new { dateKey });

        if (existing is not null)
        {
            if (date.Date != DateTime.Today) return;
            if (DateTime.UtcNow - existing.UpdatedAt < StaleAfter) return;
        }
        Build(dateKey);
    }

    private void Build(string dateKey)
    {
        var rows = _db.Query<EventRow>(@"
            SELECT app_bundle_id AS AppId, app_name AS AppName, url AS Url, is_idle AS IsIdle
            FROM activity_events
            WHERE date(timestamp, 'localtime') = @dateKey", new { dateKey }).ToList();
        if (rows.Count == 0) return;

        var active = rows.Where(r => !r.IsIdle).ToList();
        var idleSeconds = rows.Count - active.Count;

        var categorySeconds = new Dictionary<string, int>();
        var appSeconds = new Dictionary<string, (string Name, int Seconds)>();
        foreach (var row in active)
        {
            var category = _categorizer.CategoryName(row.AppId, row.Url);
            categorySeconds[category] = categorySeconds.GetValueOrDefault(category) + 1;
            var current = appSeconds.GetValueOrDefault(row.AppId);
            appSeconds[row.AppId] = (row.AppName, current.Seconds + 1);
        }

        var score = FocusScore.Compute(
            categorySeconds.GetValueOrDefault(FocusScore.DeepWorkCategory), active.Count);

        var categoryTotals = categorySeconds
            .Select(kv => new CategoryTotal(kv.Key, kv.Key, _categorizer.ColorHex(kv.Key), kv.Value))
            .OrderByDescending(c => c.Seconds).ToList();
        var topApps = appSeconds
            .Select(kv => new AppTotal(kv.Key, kv.Key, kv.Value.Name, kv.Value.Seconds))
            .OrderByDescending(a => a.Seconds).Take(20).ToList();

        Upsert(dateKey, active.Count, idleSeconds, score,
            JsonSerializer.Serialize(categoryTotals, JsonFile.Options),
            JsonSerializer.Serialize(topApps, JsonFile.Options));
    }

    private void Upsert(string dateKey, int active, int idle, double score, string categoryJson, string appsJson)
    {
        var now = DateTime.UtcNow;
        _db.Execute(@"
            INSERT INTO daily_summaries
                (date, total_active_s, total_idle_s, focus_score, category_json, top_apps_json, created_at, updated_at)
            VALUES (@dateKey, @active, @idle, @score, @categoryJson, @appsJson, @now, @now)
            ON CONFLICT(date) DO UPDATE SET
                total_active_s = excluded.total_active_s,
                total_idle_s   = excluded.total_idle_s,
                focus_score    = excluded.focus_score,
                category_json  = excluded.category_json,
                top_apps_json  = excluded.top_apps_json,
                updated_at     = excluded.updated_at",
            new { dateKey, active, idle, score, categoryJson, appsJson, now });
    }

    private sealed class EventRow
    {
        public string AppId { get; set; } = "";
        public string AppName { get; set; } = "";
        public string? Url { get; set; }
        public bool IsIdle { get; set; }
    }
}
