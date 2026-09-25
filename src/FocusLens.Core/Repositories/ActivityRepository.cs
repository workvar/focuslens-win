using FocusLens.Core.Categorization;
using FocusLens.Core.Data;
using FocusLens.Core.Models;
using FocusLens.Core.Summary;

namespace FocusLens.Core.Repositories;

/// <summary>Read-side queries for the dashboard and the AI chat.</summary>
public sealed class ActivityRepository
{
    private readonly SummaryBuilder _summaryBuilder;

    public Db Database { get; }
    public CategorizationService Categorizer { get; }

    public ActivityRepository(Db db)
    {
        Database = db;
        Categorizer = new CategorizationService(db);
        _summaryBuilder = new SummaryBuilder(db, Categorizer);
    }

    public Task<DailySummary?> FetchDailySummaryAsync(DateTime date) =>
        Database.QueryFirstOrDefaultAsync<DailySummary>(
            "SELECT * FROM daily_summaries WHERE date = @key", new { key = SummaryBuilder.DateKey(date) });

    public async Task<IReadOnlyList<DailySummary>> FetchSummariesAsync(DateTime from, DateTime to) =>
        (await Database.QueryAsync<DailySummary>(
            "SELECT * FROM daily_summaries WHERE date >= @a AND date <= @b ORDER BY date ASC",
            new { a = SummaryBuilder.DateKey(from), b = SummaryBuilder.DateKey(to) })).ToList();

    public async Task<IReadOnlyList<AppTotal>> FetchTopAppsAsync(DateTime date, int limit = 10)
    {
        var summary = await FetchDailySummaryAsync(date);
        return summary?.TopApps.Take(limit).ToList() ?? new List<AppTotal>();
    }

    public Task RebuildSummaryIfNeededAsync(DateTime date) => _summaryBuilder.BuildIfNeededAsync(date);

    public Task<IReadOnlyList<ActivityEvent>> FetchRawEventsAsync(DateTime from, DateTime to) =>
        Task.Run<IReadOnlyList<ActivityEvent>>(() => Database.Query<ActivityEvent>(
            "SELECT * FROM activity_events WHERE timestamp >= @a AND timestamp <= @b ORDER BY timestamp ASC",
            new { a = from, b = to }).ToList());

    /// <summary>Active seconds per hour of the day (0-23) over a range, in local time.</summary>
    public Task<Dictionary<int, int>> FetchHourlyActiveSecondsAsync(DateTime from, DateTime to) =>
        Task.Run(() =>
        {
            var rows = Database.Query<(long Hour, long Secs)>(@"
                SELECT CAST(strftime('%H', timestamp, 'localtime') AS INTEGER) AS Hour, COUNT(*) AS Secs
                FROM activity_events
                WHERE timestamp >= @a AND timestamp <= @b AND is_idle = 0
                GROUP BY Hour", new { a = from, b = to });
            return rows.ToDictionary(r => (int)r.Hour, r => (int)r.Secs);
        });

    /// <summary>Per-hour category breakdown for one local day.</summary>
    public Task<IReadOnlyList<HourlyBucket>> FetchHourlyBreakdownAsync(DateTime date) =>
        Task.Run<IReadOnlyList<HourlyBucket>>(() =>
        {
            var key = SummaryBuilder.DateKey(date);
            var rows = Database.Query<HourRow>(@"
                SELECT CAST(strftime('%H', timestamp, 'localtime') AS INTEGER) AS Hour,
                       app_bundle_id AS AppId, url AS Url, COUNT(*) AS Secs
                FROM activity_events
                WHERE date(timestamp, 'localtime') = @key AND is_idle = 0
                GROUP BY Hour, app_bundle_id, url", new { key }).ToList();

            return Enumerable.Range(0, 24).Select(hour =>
            {
                var perCategory = new Dictionary<string, int>();
                foreach (var row in rows.Where(r => r.Hour == hour))
                {
                    var name = Categorizer.CategoryName(row.AppId, row.Url);
                    perCategory[name] = perCategory.GetValueOrDefault(name) + (int)row.Secs;
                }
                var segments = perCategory
                    .Select(kv => new CategorySegment($"{kv.Key}-{hour}", kv.Key, Categorizer.ColorHex(kv.Key), kv.Value))
                    .OrderByDescending(s => s.Seconds).ToList();
                var bucketDate = date.Date.AddHours(hour);
                return new HourlyBucket($"{key}T{hour:00}", hour, bucketDate, segments);
            }).ToList();
        });

    /// <summary>Top window titles and URLs per app over a range, for the AI prompt.</summary>
    public Task<IReadOnlyList<AppActivityDetail>> FetchAppActivityDetailsAsync(
        DateTime from, DateTime to, int maxApps = 8, int maxItemsPerApp = 8) =>
        Task.Run<IReadOnlyList<AppActivityDetail>>(() =>
        {
            var rows = Database.Query<DetailRow>(@"
                SELECT app_name AS AppName, app_bundle_id AS AppId,
                       window_title AS Title, url AS Url, COUNT(*) AS Secs
                FROM activity_events
                WHERE timestamp >= @a AND timestamp <= @b AND is_idle = 0
                GROUP BY app_name, app_bundle_id, window_title, url
                ORDER BY Secs DESC
                LIMIT 600", new { a = from, b = to });

            var order = new List<string>();
            var names = new Dictionary<string, string>();
            var totals = new Dictionary<string, int>();
            var titles = new Dictionary<string, List<ActivityItem>>();
            var urls = new Dictionary<string, List<ActivityItem>>();

            foreach (var row in rows)
            {
                if (!totals.ContainsKey(row.AppId))
                {
                    order.Add(row.AppId);
                    names[row.AppId] = row.AppName;
                    totals[row.AppId] = 0;
                    titles[row.AppId] = new List<ActivityItem>();
                    urls[row.AppId] = new List<ActivityItem>();
                }
                var secs = (int)row.Secs;
                totals[row.AppId] += secs;
                if (!string.IsNullOrEmpty(row.Title) && titles[row.AppId].Count < maxItemsPerApp)
                    titles[row.AppId].Add(new ActivityItem(row.Title, secs));
                if (!string.IsNullOrEmpty(row.Url) && urls[row.AppId].Count < maxItemsPerApp)
                    urls[row.AppId].Add(new ActivityItem(row.Url, secs));
            }

            return order.OrderByDescending(id => totals[id]).Take(maxApps)
                .Select(id => new AppActivityDetail(id, names[id], id, totals[id], titles[id], urls[id]))
                .ToList();
        });

    private sealed class HourRow
    {
        public long Hour { get; set; }
        public string AppId { get; set; } = "";
        public string? Url { get; set; }
        public long Secs { get; set; }
    }

    private sealed class DetailRow
    {
        public string AppName { get; set; } = "";
        public string AppId { get; set; } = "";
        public string? Title { get; set; }
        public string? Url { get; set; }
        public long Secs { get; set; }
    }
}
