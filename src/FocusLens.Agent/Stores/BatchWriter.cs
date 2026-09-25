using FocusLens.Core.Data;
using FocusLens.Core.Logging;
using FocusLens.Core.Models;

namespace FocusLens.Agent.Stores;

/// <summary>
/// Buffers one-second activity events and writes them in a single transaction every five seconds.
/// Events are re-checked against the privacy filter at write time, so a category enabled a moment
/// ago also covers events already in the buffer.
/// </summary>
public sealed class BatchWriter : IDisposable
{
    private readonly Db _db;
    private readonly PrivacyFilter _privacy;
    private readonly FileLog _log;
    private readonly object _gate = new();
    private readonly List<ActivityEvent> _buffer = new();
    private readonly Timer _timer;

    public BatchWriter(Db db, PrivacyFilter privacy, FileLog log)
    {
        _db = db;
        _privacy = privacy;
        _log = log;
        _timer = new Timer(_ => Flush(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public void Enqueue(ActivityEvent evt)
    {
        lock (_gate) _buffer.Add(evt);
    }

    public void Flush()
    {
        List<ActivityEvent> batch;
        lock (_gate)
        {
            if (_buffer.Count == 0) return;
            batch = new List<ActivityEvent>(_buffer);
            _buffer.Clear();
        }

        try
        {
            var allowed = batch.Where(e => !_privacy.IsExcluded(e.AppBundleId, e.Url, e.WindowTitle)).ToList();
            if (allowed.Count == 0) return;

            _db.InTransaction((connection, transaction) => Dapper.SqlMapper.Execute(connection, @"
                INSERT INTO activity_events (timestamp, app_bundle_id, app_name, window_title, url, is_idle, category_id, created_at)
                VALUES (@Timestamp, @AppBundleId, @AppName, @WindowTitle, @Url, @IsIdle, @CategoryId, @CreatedAt)",
                allowed, transaction));
        }
        catch (Exception ex)
        {
            _log.Error("Batch write failed; will retry", ex);
            lock (_gate) _buffer.InsertRange(0, batch);
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
        Flush();
    }
}
