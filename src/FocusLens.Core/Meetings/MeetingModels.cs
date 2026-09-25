namespace FocusLens.Core.Meetings;

public sealed class Meeting
{
    public string Id { get; set; } = "";
    public string? ProjectId { get; set; }
    public string Title { get; set; } = "";
    public string Provider { get; set; } = "unknown";
    public string Source { get; set; } = "manual";
    public long StartedAt { get; set; }
    public long? EndedAt { get; set; }
    public int DurationS { get; set; }
    public string Phase { get; set; } = "recording";
    public string? CalendarEventId { get; set; }
    public string? RecurrenceId { get; set; }
    public string? AttendeesJson { get; set; }
    public string? ConferencingUrl { get; set; }
    public string? AudioDir { get; set; }
    public long? AudioDeletedAt { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }

    public static Meeting New(string id, MeetingCandidate candidate, DateTime startedAt, string? audioDir)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return new Meeting
        {
            Id = id,
            Title = candidate.ResolvedTitle,
            Provider = candidate.Provider.Id(),
            Source = candidate.Source.ToString().ToLowerInvariant(),
            StartedAt = new DateTimeOffset(startedAt.ToUniversalTime()).ToUnixTimeSeconds(),
            Phase = "recording",
            ConferencingUrl = candidate.ConferencingUrl,
            AudioDir = audioDir,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}

public enum TranscriptTrack
{
    Mic,
    System,
}

public static class TranscriptTrackExtensions
{
    public static string Id(this TranscriptTrack track) => track == TranscriptTrack.Mic ? "mic" : "system";
    public static string SpeakerLabel(this TranscriptTrack track) => track == TranscriptTrack.Mic ? "You" : "Others";
    public static TranscriptTrack FromId(string id) => id == "mic" ? TranscriptTrack.Mic : TranscriptTrack.System;
}

public sealed class TranscriptSegment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MeetingId { get; set; } = "";
    public string Track { get; set; } = "system";
    public int StartMs { get; set; }
    public int EndMs { get; set; }
    public string Text { get; set; } = "";
    public double? Confidence { get; set; }
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}

public enum NoteBlockType
{
    Summary,
    Decisions,
    ActionItems,
    OpenQuestions,
    Transcript,
}

public static class NoteBlockTypeExtensions
{
    public static string Id(this NoteBlockType type) => type switch
    {
        NoteBlockType.ActionItems => "action_items",
        NoteBlockType.OpenQuestions => "open_questions",
        _ => type.ToString().ToLowerInvariant(),
    };

    public static NoteBlockType FromId(string id) => id switch
    {
        "action_items" => NoteBlockType.ActionItems,
        "open_questions" => NoteBlockType.OpenQuestions,
        "decisions" => NoteBlockType.Decisions,
        "transcript" => NoteBlockType.Transcript,
        _ => NoteBlockType.Summary,
    };

    public static string Heading(this NoteBlockType type) => type switch
    {
        NoteBlockType.Summary => "Summary",
        NoteBlockType.Decisions => "Decisions",
        NoteBlockType.ActionItems => "Action items",
        NoteBlockType.OpenQuestions => "Open questions",
        _ => "Transcript",
    };

    public static int Position(this NoteBlockType type) => (int)type;
}

public sealed class MeetingNote
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MeetingId { get; set; } = "";
    public string BlockType { get; set; } = "summary";
    public int Position { get; set; }
    public string ContentMd { get; set; } = "";
    public string? MetaJson { get; set; }
    public long? EditedAt { get; set; }
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public static MeetingNote Make(string meetingId, NoteBlockType type, string contentMd) => new()
    {
        MeetingId = meetingId,
        BlockType = type.Id(),
        Position = type.Position(),
        ContentMd = contentMd,
    };
}

public enum ActionStatus
{
    Open,
    Done,
    Dropped,
}

public sealed class ActionItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MeetingId { get; set; } = "";
    public string? ProjectId { get; set; }
    public string? Owner { get; set; }
    public string Text { get; set; } = "";
    public long? DueAt { get; set; }
    public string Status { get; set; } = "open";
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public long? CompletedAt { get; set; }

    public static ActionItem Make(string meetingId, string? owner, string text) =>
        new() { MeetingId = meetingId, Owner = owner, Text = text };
}

public sealed class ActionItemWithMeeting
{
    public ActionItem Item { get; set; } = new();
    public string MeetingTitle { get; set; } = "";
    public long MeetingStartedAt { get; set; }
}
