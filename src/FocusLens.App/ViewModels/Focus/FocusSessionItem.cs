using FocusLens.Core.Focus;

namespace FocusLens.App.ViewModels.Focus;

/// <summary>One saved session in the list, and the "just finished" summary card.</summary>
public sealed class FocusSessionItem
{
    public FocusSessionItem(FocusSessionRecord record) => Record = record;

    public FocusSessionRecord Record { get; }
    public int Score => Record.Score;
    public string Goal => Record.Goal;
    public string When => $"{FocusFormat.Day(Record.StartedAt)}, {FocusFormat.Duration(Record.Duration())}";
    public string DistractionsText => FocusFormat.Plural(Record.DistractionCount(), "distraction");

    public string SummaryTitle => Record.Completed ? "Session complete" : "Session ended early";

    public string SummaryText =>
        $"{FocusFormat.Duration(Record.FocusedSeconds)} focused, {DistractionsText}, {FocusFormat.Plural(Record.NudgeCount(), "nudge")}";
}
