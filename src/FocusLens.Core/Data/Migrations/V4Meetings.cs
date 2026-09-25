namespace FocusLens.Core.Data.Migrations;

/// <summary>
/// Meetings: sessions, transcript segments, structured note blocks, and action items.
/// Action items are a table because they outlive the meeting.
/// </summary>
internal static class V4Meetings
{
    public const string Sql = @"
CREATE TABLE IF NOT EXISTS meetings (
    id                TEXT PRIMARY KEY,
    project_id        TEXT,
    title             TEXT NOT NULL,
    provider          TEXT NOT NULL,
    source            TEXT NOT NULL,
    started_at        INTEGER NOT NULL,
    ended_at          INTEGER,
    duration_s        INTEGER NOT NULL DEFAULT 0,
    phase             TEXT NOT NULL,
    calendar_event_id TEXT,
    recurrence_id     TEXT,
    attendees_json    TEXT,
    conferencing_url  TEXT,
    audio_dir         TEXT,
    audio_deleted_at  INTEGER,
    created_at        INTEGER NOT NULL,
    updated_at        INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_meetings_started ON meetings(started_at DESC);
CREATE INDEX IF NOT EXISTS idx_meetings_project ON meetings(project_id, started_at DESC);

CREATE TABLE IF NOT EXISTS transcript_segments (
    id         TEXT PRIMARY KEY,
    meeting_id TEXT NOT NULL REFERENCES meetings(id) ON DELETE CASCADE,
    track      TEXT NOT NULL,
    start_ms   INTEGER NOT NULL,
    end_ms     INTEGER NOT NULL,
    text       TEXT NOT NULL,
    confidence REAL,
    created_at INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_segments_meeting ON transcript_segments(meeting_id, start_ms);

CREATE TABLE IF NOT EXISTS meeting_notes (
    id         TEXT PRIMARY KEY,
    meeting_id TEXT NOT NULL REFERENCES meetings(id) ON DELETE CASCADE,
    block_type TEXT NOT NULL,
    position   INTEGER NOT NULL DEFAULT 0,
    content_md TEXT NOT NULL,
    meta_json  TEXT,
    edited_at  INTEGER,
    created_at INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_notes_meeting ON meeting_notes(meeting_id, position);

CREATE TABLE IF NOT EXISTS action_items (
    id           TEXT PRIMARY KEY,
    meeting_id   TEXT NOT NULL REFERENCES meetings(id) ON DELETE CASCADE,
    project_id   TEXT,
    owner        TEXT,
    text         TEXT NOT NULL,
    due_at       INTEGER,
    status       TEXT NOT NULL DEFAULT 'open',
    created_at   INTEGER NOT NULL,
    completed_at INTEGER
);
CREATE INDEX IF NOT EXISTS idx_actions_status ON action_items(status, owner);
";
}
