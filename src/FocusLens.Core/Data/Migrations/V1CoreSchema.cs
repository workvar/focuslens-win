namespace FocusLens.Core.Data.Migrations;

/// <summary>Core tracking schema: categories, rules, events, daily summaries, settings.</summary>
internal static class V1CoreSchema
{
    public const string Sql = @"
CREATE TABLE IF NOT EXISTS categories (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    name       TEXT NOT NULL UNIQUE,
    color_hex  TEXT NOT NULL,
    is_system  INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS app_rules (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    match_type    TEXT NOT NULL,
    match_value   TEXT NOT NULL,
    category_id   INTEGER REFERENCES categories(id) ON DELETE SET NULL,
    is_excluded   INTEGER NOT NULL DEFAULT 0,
    user_override INTEGER NOT NULL DEFAULT 0,
    created_at    TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS activity_events (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp     TEXT NOT NULL,
    app_bundle_id TEXT NOT NULL,
    app_name      TEXT NOT NULL,
    window_title  TEXT,
    url           TEXT,
    is_idle       INTEGER NOT NULL DEFAULT 0,
    category_id   INTEGER REFERENCES categories(id) ON DELETE SET NULL,
    created_at    TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_events_timestamp ON activity_events(timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_events_app ON activity_events(app_bundle_id);

CREATE TABLE IF NOT EXISTS daily_summaries (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    date           TEXT NOT NULL UNIQUE,
    total_active_s INTEGER NOT NULL,
    total_idle_s   INTEGER NOT NULL,
    focus_score    REAL NOT NULL,
    category_json  TEXT NOT NULL,
    top_apps_json  TEXT NOT NULL,
    created_at     TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at     TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS settings (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

INSERT OR IGNORE INTO categories (name, color_hex, is_system) VALUES
    ('Deep Work',     '#1A56A0', 1),
    ('Communication', '#0E8A7A', 1),
    ('Meetings',      '#7C3AED', 1),
    ('Social Media',  '#DC2626', 1),
    ('News',          '#D97706', 1),
    ('Utilities',     '#64748B', 1),
    ('Other',         '#9CA3AF', 1);
";
}
