using FocusLens.Core.Data;

namespace FocusLens.Core.Health;

/// <summary>Opens the app's own database connection and runs a trivial query.</summary>
public sealed class SqliteProbe : IHealthProbe
{
    private readonly Db _db;

    public SqliteProbe(Db db) => _db = db;

    public string Name => "SQLite";
    public TimeSpan MinInterval => TimeSpan.FromSeconds(4);

    public async Task<ServiceHealth> CheckAsync(CancellationToken ct)
    {
        try
        {
            var schemas = await _db.QueryFirstOrDefaultAsync<long>("SELECT COUNT(*) FROM schema_migrations");
            var size = File.Exists(_db.Path) ? new FileInfo(_db.Path).Length : 0;
            return ServiceHealth.Connected(Name, $"{System.IO.Path.GetFileName(_db.Path)}, {FormatSize(size)}, {schemas} migrations applied");
        }
        catch (Exception ex)
        {
            return ServiceHealth.Disconnected(Name, $"Cannot read {System.IO.Path.GetFileName(_db.Path)}: {ex.Message}");
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 * 1024 => $"{Math.Max(1, bytes / 1024)} KB",
        _ => $"{bytes / (1024.0 * 1024.0):0.#} MB",
    };
}
