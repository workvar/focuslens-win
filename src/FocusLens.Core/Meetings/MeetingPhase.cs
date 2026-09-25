namespace FocusLens.Core.Meetings;

/// <summary>The state machine's phases. Records give structural equality for change detection.</summary>
public abstract record MeetingPhase
{
    public sealed record Idle : MeetingPhase;
    public sealed record Detected(MeetingCandidate Candidate) : MeetingPhase;
    public sealed record Recording : MeetingPhase;
    public sealed record Paused : MeetingPhase;
    public sealed record Transcribing(double Progress) : MeetingPhase;
    public sealed record Summarizing : MeetingPhase;
    public sealed record Complete(string MeetingId) : MeetingPhase;
    public sealed record Failed(MeetingError Error) : MeetingPhase;

    public bool IsActive => this is not Idle;
    public bool IsCapturing => this is Recording;
    public bool IsProcessing => this is Transcribing or Summarizing;
    public bool IsTerminal => this is Complete or Failed;
}

/// <summary>Everything a status surface (window, tray icon) needs to render the session.</summary>
public sealed record MeetingStatusSnapshot
{
    public MeetingPhase Phase { get; init; } = new MeetingPhase.Idle();
    public string? MeetingId { get; init; }
    public string Title { get; init; } = "";
    public MeetingProvider Provider { get; init; } = MeetingProvider.Unknown;
    public MeetingSource Source { get; init; } = MeetingSource.Manual;
    public DateTime? StartedAt { get; init; }
    public TimeSpan PausedTotal { get; init; }
    public DateTime? PausedSince { get; init; }

    public static MeetingStatusSnapshot Idle { get; } = new();
}

public interface IMeetingStatusSink
{
    void Receive(MeetingStatusSnapshot snapshot);
}
