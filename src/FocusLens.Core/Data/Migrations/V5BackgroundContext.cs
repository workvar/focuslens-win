namespace FocusLens.Core.Data.Migrations;

/// <summary>Screen text from unfocused windows, plus open browser tabs.</summary>
internal static class V5BackgroundContext
{
    public const string Sql = @"
ALTER TABLE screenshots ADD COLUMN focus_state TEXT NOT NULL DEFAULT 'foreground';

CREATE TABLE IF NOT EXISTS open_tabs (
    id        INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp TEXT NOT NULL,
    browser   TEXT NOT NULL,
    title     TEXT,
    url       TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_open_tabs_timestamp ON open_tabs(timestamp);
";
}
