namespace FocusLens.Core.Focus;

public enum EpisodeOutcome
{
    Returned,
    Closed,
    Blocked,
    Snoozed,
    CloseFailed,
    SessionEnded,
}

/// <summary>One stretch of time on one off-topic site or app.</summary>
public sealed class DistractionEpisode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public double Seconds { get; set; }
    public bool Nudged { get; set; }
    public EpisodeOutcome Outcome { get; set; } = EpisodeOutcome.Returned;
}

/// <summary>One point of the live score, for the timeline chart. Offset is seconds since the start.</summary>
public sealed record FocusScorePoint(double Offset, double Score);

/// <summary>
/// What is saved for every finished session. Everything the insights screen shows is derived
/// from these, so nothing else needs to be stored.
/// </summary>
public sealed class FocusSessionRecord
{
    public Guid Id { get; set; }
    public string Goal { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public int PlannedSeconds { get; set; }
    public FocusEnforcement Enforcement { get; set; }
    public bool Completed { get; set; }
    public double FocusedSeconds { get; set; }
    public double DistractedSeconds { get; set; }
    public double NeutralSeconds { get; set; }
    /// <summary>Focused share of judged time, 0 to 100.</summary>
    public int Score { get; set; }
    public List<DistractionEpisode> Episodes { get; set; } = new();
    public List<FocusScorePoint> Timeline { get; set; } = new();
}

public sealed record DistractionSummary(string Label, double Seconds, int Count);
