namespace FocusLens.Core.Data.Migrations;

/// <summary>Adds persisted pin state for the recent chat list.</summary>
internal static class V7ConversationPins
{
    public const string Sql = @"
ALTER TABLE conversations ADD COLUMN is_pinned INTEGER NOT NULL DEFAULT 0;
CREATE INDEX IF NOT EXISTS idx_conversations_pinned_updated
    ON conversations(is_pinned DESC, updated_at DESC) WHERE archived_at IS NULL;
";
}