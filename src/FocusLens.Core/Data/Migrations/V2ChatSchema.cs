namespace FocusLens.Core.Data.Migrations;

/// <summary>Multi-conversation persistent chat.</summary>
internal static class V2ChatSchema
{
    public const string Sql = @"
CREATE TABLE IF NOT EXISTS conversations (
    id                 TEXT PRIMARY KEY,
    title              TEXT NOT NULL,
    created_at         INTEGER NOT NULL,
    updated_at         INTEGER NOT NULL,
    archived_at        INTEGER,
    supabase_synced_at INTEGER
);
CREATE INDEX IF NOT EXISTS idx_conversations_updated
    ON conversations(updated_at DESC) WHERE archived_at IS NULL;

CREATE TABLE IF NOT EXISTS messages (
    id              TEXT PRIMARY KEY,
    conversation_id TEXT NOT NULL REFERENCES conversations(id) ON DELETE CASCADE,
    role            TEXT NOT NULL,
    content_md      TEXT NOT NULL,
    chart_json      TEXT,
    created_at      INTEGER NOT NULL,
    token_count     INTEGER
);
CREATE INDEX IF NOT EXISTS idx_messages_conv ON messages(conversation_id, created_at);
";
}
