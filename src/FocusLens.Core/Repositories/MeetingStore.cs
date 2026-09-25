using FocusLens.Core.Data;
using FocusLens.Core.Meetings;

namespace FocusLens.Core.Repositories;

/// <summary>Persistence for meetings, transcripts, note blocks and action items.</summary>
public sealed class MeetingStore
{
    private readonly Db _db;

    public MeetingStore(Db db) => _db = db;

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    // Meetings

    public Task CreateAsync(Meeting meeting) => _db.ExecuteAsync(@"
        INSERT INTO meetings (id, project_id, title, provider, source, started_at, ended_at, duration_s, phase,
            calendar_event_id, recurrence_id, attendees_json, conferencing_url, audio_dir, audio_deleted_at,
            created_at, updated_at)
        VALUES (@Id, @ProjectId, @Title, @Provider, @Source, @StartedAt, @EndedAt, @DurationS, @Phase,
            @CalendarEventId, @RecurrenceId, @AttendeesJson, @ConferencingUrl, @AudioDir, @AudioDeletedAt,
            @CreatedAt, @UpdatedAt)", meeting);

    public Task<Meeting?> FetchAsync(string id) =>
        _db.QueryFirstOrDefaultAsync<Meeting>("SELECT * FROM meetings WHERE id = @id", new { id });

    public async Task<IReadOnlyList<Meeting>> RecentAsync(int limit = 50) =>
        (await _db.QueryAsync<Meeting>("SELECT * FROM meetings ORDER BY started_at DESC LIMIT @limit", new { limit })).ToList();

    public Task FinishAsync(string id, string phase, int durationS, DateTime? endedAt = null)
    {
        var ended = new DateTimeOffset((endedAt ?? DateTime.UtcNow).ToUniversalTime()).ToUnixTimeSeconds();
        return _db.ExecuteAsync(
            "UPDATE meetings SET phase = @phase, duration_s = @durationS, ended_at = @ended, updated_at = @ended WHERE id = @id",
            new { id, phase, durationS, ended });
    }

    /// <summary>Meetings left in "recording" by a crash or quit are marked interrupted.</summary>
    public async Task<IReadOnlyList<Meeting>> MarkOrphanedAsInterruptedAsync()
    {
        var orphans = (await _db.QueryAsync<Meeting>("SELECT * FROM meetings WHERE phase = 'recording'")).ToList();
        if (orphans.Count > 0)
            await _db.ExecuteAsync("UPDATE meetings SET phase = 'interrupted' WHERE phase = 'recording'");
        return orphans;
    }

    public Task ClearAudioAsync(string id) => _db.ExecuteAsync(
        "UPDATE meetings SET audio_dir = NULL, audio_deleted_at = @now WHERE id = @id", new { id, now = Now() });

    public Task DeleteMeetingAsync(string id) =>
        _db.ExecuteAsync("DELETE FROM meetings WHERE id = @id", new { id });

    // Transcript

    public Task AppendSegmentsAsync(IReadOnlyList<TranscriptSegment> segments)
    {
        if (segments.Count == 0) return Task.CompletedTask;
        return Task.Run(() => _db.InTransaction((connection, transaction) =>
            Dapper.SqlMapper.Execute(connection, @"
                INSERT INTO transcript_segments (id, meeting_id, track, start_ms, end_ms, text, confidence, created_at)
                VALUES (@Id, @MeetingId, @Track, @StartMs, @EndMs, @Text, @Confidence, @CreatedAt)",
                segments, transaction)));
    }

    public Task DeleteSegmentsAsync(string meetingId) =>
        _db.ExecuteAsync("DELETE FROM transcript_segments WHERE meeting_id = @meetingId", new { meetingId });

    public async Task<IReadOnlyList<TranscriptSegment>> SegmentsAsync(string meetingId) =>
        (await _db.QueryAsync<TranscriptSegment>(
            "SELECT * FROM transcript_segments WHERE meeting_id = @meetingId ORDER BY start_ms ASC",
            new { meetingId })).ToList();

    public async Task<string> TranscriptTextAsync(string meetingId)
    {
        var segments = await SegmentsAsync(meetingId);
        return string.Join("\n", segments.Select(s =>
            $"{TranscriptTrackExtensions.FromId(s.Track).SpeakerLabel()}: {s.Text}"));
    }

    // Notes

    /// <summary>Replaces generated blocks but never one the user has edited.</summary>
    public Task UpsertNotesAsync(IReadOnlyList<MeetingNote> notes)
    {
        if (notes.Count == 0) return Task.CompletedTask;
        var meetingId = notes[0].MeetingId;
        return Task.Run(() => _db.InTransaction((connection, transaction) =>
        {
            var protectedTypes = Dapper.SqlMapper.Query<string>(connection,
                "SELECT block_type FROM meeting_notes WHERE meeting_id = @meetingId AND edited_at IS NOT NULL",
                new { meetingId }, transaction).ToHashSet();

            foreach (var note in notes.Where(n => !protectedTypes.Contains(n.BlockType)))
            {
                Dapper.SqlMapper.Execute(connection,
                    "DELETE FROM meeting_notes WHERE meeting_id = @MeetingId AND block_type = @BlockType AND edited_at IS NULL",
                    note, transaction);
                Dapper.SqlMapper.Execute(connection, @"
                    INSERT INTO meeting_notes (id, meeting_id, block_type, position, content_md, meta_json, edited_at, created_at)
                    VALUES (@Id, @MeetingId, @BlockType, @Position, @ContentMd, @MetaJson, @EditedAt, @CreatedAt)",
                    note, transaction);
            }
        }));
    }

    public Task UpdateNoteAsync(string id, string contentMd) => _db.ExecuteAsync(
        "UPDATE meeting_notes SET content_md = @contentMd, edited_at = @now WHERE id = @id",
        new { id, contentMd, now = Now() });

    public Task RevertNoteAsync(string id) =>
        _db.ExecuteAsync("UPDATE meeting_notes SET edited_at = NULL WHERE id = @id", new { id });

    public async Task<IReadOnlyList<MeetingNote>> NotesAsync(string meetingId) =>
        (await _db.QueryAsync<MeetingNote>(
            "SELECT * FROM meeting_notes WHERE meeting_id = @meetingId ORDER BY position ASC",
            new { meetingId })).ToList();

    // Action items

    /// <summary>Replaces the open items of a meeting; done and dropped ones are kept.</summary>
    public Task ReplaceActionItemsAsync(IReadOnlyList<ActionItem> items, string meetingId) =>
        Task.Run(() => _db.InTransaction((connection, transaction) =>
        {
            Dapper.SqlMapper.Execute(connection,
                "DELETE FROM action_items WHERE meeting_id = @meetingId AND status = 'open'", new { meetingId }, transaction);
            if (items.Count > 0)
                Dapper.SqlMapper.Execute(connection, @"
                    INSERT INTO action_items (id, meeting_id, project_id, owner, text, due_at, status, created_at, completed_at)
                    VALUES (@Id, @MeetingId, @ProjectId, @Owner, @Text, @DueAt, @Status, @CreatedAt, @CompletedAt)",
                    items, transaction);
        }));

    public async Task<IReadOnlyList<ActionItem>> ActionItemsAsync(string meetingId) =>
        (await _db.QueryAsync<ActionItem>(
            "SELECT * FROM action_items WHERE meeting_id = @meetingId ORDER BY created_at ASC",
            new { meetingId })).ToList();

    public Task SetActionItemStatusAsync(string id, ActionStatus status)
    {
        long? completedAt = status == ActionStatus.Done ? Now() : null;
        return _db.ExecuteAsync(
            "UPDATE action_items SET status = @status, completed_at = @completedAt WHERE id = @id",
            new { id, status = status.ToString().ToLowerInvariant(), completedAt });
    }

    public async Task<IReadOnlyList<ActionItemWithMeeting>> AllActionItemsAsync(bool includeDone = false, int limit = 200)
    {
        var where = includeDone ? "" : "WHERE a.status = 'open'";
        var rows = await _db.QueryAsync<ActionRow>($@"
            SELECT a.id AS Id, a.meeting_id AS MeetingId, a.project_id AS ProjectId, a.owner AS Owner, a.text AS Text,
                   a.due_at AS DueAt, a.status AS Status, a.created_at AS CreatedAt, a.completed_at AS CompletedAt,
                   m.title AS MeetingTitle, m.started_at AS MeetingStartedAt
            FROM action_items a JOIN meetings m ON m.id = a.meeting_id
            {where}
            ORDER BY a.status ASC, m.started_at DESC
            LIMIT {limit}");
        return rows.Select(r => new ActionItemWithMeeting
        {
            Item = new ActionItem
            {
                Id = r.Id, MeetingId = r.MeetingId, ProjectId = r.ProjectId, Owner = r.Owner, Text = r.Text,
                DueAt = r.DueAt, Status = r.Status, CreatedAt = r.CreatedAt, CompletedAt = r.CompletedAt,
            },
            MeetingTitle = r.MeetingTitle,
            MeetingStartedAt = r.MeetingStartedAt,
        }).ToList();
    }

    private sealed class ActionRow
    {
        public string Id { get; set; } = "";
        public string MeetingId { get; set; } = "";
        public string? ProjectId { get; set; }
        public string? Owner { get; set; }
        public string Text { get; set; } = "";
        public long? DueAt { get; set; }
        public string Status { get; set; } = "open";
        public long CreatedAt { get; set; }
        public long? CompletedAt { get; set; }
        public string MeetingTitle { get; set; } = "";
        public long MeetingStartedAt { get; set; }
    }
}
