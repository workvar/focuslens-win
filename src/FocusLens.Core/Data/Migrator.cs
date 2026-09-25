using Dapper;
using FocusLens.Core.Data.Migrations;

namespace FocusLens.Core.Data;

/// <summary>Numbered, idempotent schema migrations tracked in schema_migrations.</summary>
public static class Migrator
{
    private static readonly (string Id, string Sql)[] All =
    {
        ("v1_core_schema", V1CoreSchema.Sql),
        ("v2_chat_schema", V2ChatSchema.Sql),
        ("v3_screenshots", V3Screenshots.Sql),
        ("v4_meetings_schema", V4Meetings.Sql),
        ("v5_background_context", V5BackgroundContext.Sql),
        ("v6_activity_signals", V6ActivitySignals.Sql),
        ("v7_conversation_pins", V7ConversationPins.Sql),
    };

    public static void Run(Db db)
    {
        using var connection = db.Open();
        connection.Execute("CREATE TABLE IF NOT EXISTS schema_migrations (id TEXT PRIMARY KEY, applied_at TEXT NOT NULL)");
        var applied = connection.Query<string>("SELECT id FROM schema_migrations").ToHashSet();

        foreach (var (id, sql) in All)
        {
            if (applied.Contains(id)) continue;
            using var transaction = connection.BeginTransaction();
            connection.Execute(sql, transaction: transaction);
            connection.Execute(
                "INSERT INTO schema_migrations (id, applied_at) VALUES (@id, @at)",
                new { id, at = DbTime.ToDb(DateTime.UtcNow) }, transaction);
            transaction.Commit();
        }
    }
}
