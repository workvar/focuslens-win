using Dapper;
using Microsoft.Data.Sqlite;

namespace FocusLens.Core.Data;

/// <summary>
/// SQLite access. Each call opens a short-lived pooled connection, so the agent
/// (writer) and the app (reader) can share one database file safely in WAL mode.
/// </summary>
public sealed class Db
{
    private readonly string _connectionString;

    public string Path { get; }

    public Db(string path)
    {
        DbTime.Register();
        Path = path;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = true,
        }.ToString();
    }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        connection.Execute("PRAGMA busy_timeout = 5000; PRAGMA foreign_keys = ON;");
        return connection;
    }

    /// <summary>Creates the file if needed, switches to WAL, and applies pending migrations.</summary>
    public void Initialize()
    {
        using (var connection = Open())
            connection.Execute("PRAGMA journal_mode = WAL;");
        Migrator.Run(this);
    }

    public IEnumerable<T> Query<T>(string sql, object? param = null)
    {
        using var connection = Open();
        return connection.Query<T>(sql, param).ToList();
    }

    public T? QueryFirstOrDefault<T>(string sql, object? param = null)
    {
        using var connection = Open();
        return connection.QueryFirstOrDefault<T>(sql, param);
    }

    public int Execute(string sql, object? param = null)
    {
        using var connection = Open();
        return connection.Execute(sql, param);
    }

    public T ExecuteScalar<T>(string sql, object? param = null)
    {
        using var connection = Open();
        return connection.ExecuteScalar<T>(sql, param)!;
    }

    public long InsertReturningId(string sql, object? param = null)
    {
        using var connection = Open();
        return connection.ExecuteScalar<long>(sql + "; SELECT last_insert_rowid();", param);
    }

    /// <summary>Runs several statements in one transaction.</summary>
    public void InTransaction(Action<SqliteConnection, SqliteTransaction> work)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        work(connection, transaction);
        transaction.Commit();
    }

    public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null) =>
        Task.Run(() => Query<T>(sql, param));

    public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null) =>
        Task.Run(() => QueryFirstOrDefault<T>(sql, param));

    public Task<int> ExecuteAsync(string sql, object? param = null) =>
        Task.Run(() => Execute(sql, param));
}
