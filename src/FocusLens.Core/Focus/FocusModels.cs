namespace FocusLens.Core.Focus;

public enum FocusVerdict
{
    OnTopic,
    OffTopic,
    /// <summary>The model was unreachable or unclear. Neutral: Focus Mode never acts because it could not decide.</summary>
    Unknown,
}

public enum FocusObservationKind
{
    Focused,
    Off,
    /// <summary>Nothing to judge (FocusLens, the desktop), or the model could not tell.</summary>
    Neutral,
}

/// <summary>The monitor's latest reading of the foreground window. <see cref="Context"/> is set for Off.</summary>
public sealed record FocusObservation(FocusObservationKind Kind, FocusContext? Context = null)
{
    public static FocusObservation Focused { get; } = new(FocusObservationKind.Focused);
    public static FocusObservation Neutral { get; } = new(FocusObservationKind.Neutral);
    public static FocusObservation OffOn(FocusContext context) => new(FocusObservationKind.Off, context);
}

/// <summary>What happens when the patience bar runs out. Chosen on the start screen.</summary>
public enum FocusEnforcement
{
    Close,
    Block,
}

public static class FocusEnforcementText
{
    public static string Title(this FocusEnforcement mode) => mode == FocusEnforcement.Block ? "Block it" : "Close it";

    public static string Summary(this FocusEnforcement mode) => mode == FocusEnforcement.Block
        ? "Covers it with an overlay until you close it yourself."
        : "Closes the tab or window after a short warning.";

    /// <summary>"closed" or "blocked", for sentences like "the tab is closed when the bar empties".</summary>
    public static string Fate(this FocusEnforcement mode) => mode == FocusEnforcement.Block ? "blocked" : "closed";
}

public sealed record FocusSession(
    Guid Id, string Goal, DateTime StartedAt, TimeSpan Duration, FocusEnforcement Enforcement, TimeSpan Patience)
{
    public DateTime EndsAt => StartedAt + Duration;

    public TimeSpan Remaining(DateTime now) => EndsAt > now ? EndsAt - now : TimeSpan.Zero;
}

/// <summary>The patience bar while the user is off topic. Level 1 is full, 0 is empty.</summary>
public sealed record Distraction(string Label, bool IsBrowser, double Level, bool Nudged, int SecondsLeft);

/// <summary>Transient messages that are not driven by the patience bar.</summary>
public abstract record FocusAlert
{
    public sealed record Countdown(string Label, bool IsBrowser, int SecondsLeft) : FocusAlert;
    public sealed record Closed(string Label) : FocusAlert;
    public sealed record CouldNotClose(string Label) : FocusAlert;
    public sealed record Finished(string Goal) : FocusAlert;
}
