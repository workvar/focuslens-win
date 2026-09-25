namespace FocusLens.Core.Data.Migrations;

/// <summary>On-screen text context (OCR text; thumbnails are optional and never full resolution).</summary>
internal static class V3Screenshots
{
    public const string Sql = @"
CREATE TABLE IF NOT EXISTS screenshots (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp     TEXT NOT NULL,
    app_bundle_id TEXT NOT NULL,
    app_name      TEXT NOT NULL,
    window_title  TEXT,
    ocr_text      TEXT,
    thumb_path    TEXT,
    created_at    TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_screenshots_timestamp ON screenshots(timestamp);
";
}
