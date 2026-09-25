namespace FocusLens.Core.Data.Migrations;

/// <summary>
/// Counts and events only. Input rows hold how many keys, clicks and scrolls
/// happened, never which keys. Clipboard rows never hold content.
/// </summary>
internal static class V6ActivitySignals
{
    public const string Sql = @"
CREATE TABLE IF NOT EXISTS input_activity (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp     TEXT NOT NULL,
    app_bundle_id TEXT NOT NULL,
    app_name      TEXT NOT NULL,
    keys          INTEGER NOT NULL,
    clicks        INTEGER NOT NULL,
    scrolls       INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_input_timestamp ON input_activity(timestamp);

CREATE TABLE IF NOT EXISTS system_events (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp     TEXT NOT NULL,
    kind          TEXT NOT NULL,
    app_bundle_id TEXT,
    detail        TEXT
);
CREATE INDEX IF NOT EXISTS idx_system_events_timestamp ON system_events(timestamp);

CREATE TABLE IF NOT EXISTS document_log (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp     TEXT NOT NULL,
    app_bundle_id TEXT NOT NULL,
    app_name      TEXT NOT NULL,
    path          TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_document_log_timestamp ON document_log(timestamp);
";
}
