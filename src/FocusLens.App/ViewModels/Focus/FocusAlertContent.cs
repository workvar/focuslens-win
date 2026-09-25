using FocusLens.Core.Focus;

namespace FocusLens.App.ViewModels.Focus;

/// <summary>Colour role for focus surfaces. Mapped to theme brushes by FocusToneToBrushConverter.</summary>
public enum FocusTone
{
    Muted,
    Accent,
    Success,
    Warning,
    Danger,
}

/// <summary>
/// What the compact drawer under the floating widget says, chosen from the current state.
/// Kept apart from the view so the wording is in one place.
/// </summary>
/// <param name="Glyph">Segoe MDL2 Assets glyph, shown when there is no countdown.</param>
/// <param name="Caption">Small text at the right of the title row, such as "38s".</param>
/// <param name="Level">When set, the drawer shows the patience bar.</param>
/// <param name="Countdown">When set, the drawer shows a countdown number instead of the glyph.</param>
public sealed record FocusAlertContent(
    string Glyph, FocusTone Tone, string Title, string? Detail, string? Caption,
    double? Level, int? Countdown, bool ShowsSnooze)
{
    private const string TimerGlyph = "";
    private const string CheckGlyph = "";
    private const string WarningGlyph = "";
    private const string FlagGlyph = "";
    private const string EyeGlyph = "";

    public bool HasLevel => Level is not null;
    public bool HasCountdown => Countdown is not null;

    public static FocusAlertContent? Make(FocusAlert? alert, Distraction? distraction, string? goal, FocusEnforcement? enforcement)
    {
        if (alert is not null) return ForAlert(alert);
        return distraction is null ? null : ForDistraction(distraction, goal ?? "your goal", enforcement ?? FocusEnforcement.Close);
    }

    private static FocusAlertContent ForAlert(FocusAlert alert) => alert switch
    {
        FocusAlert.Countdown c => new(TimerGlyph, FocusTone.Danger,
            $"Closing {(c.IsBrowser ? "tab" : "window")} in {c.SecondsLeft}s", $"{c.Label} is off track",
            null, null, c.SecondsLeft, ShowsSnooze: true),
        FocusAlert.Closed c => new(CheckGlyph, FocusTone.Success, $"Closed {c.Label}", "Back to work",
            null, null, null, false),
        FocusAlert.CouldNotClose c => new(WarningGlyph, FocusTone.Warning, $"Could not close {c.Label}",
            "It may be running as administrator", null, null, null, false),
        FocusAlert.Finished f => new(FlagGlyph, FocusTone.Success, "Session complete", f.Goal,
            null, null, null, false),
        _ => new(CheckGlyph, FocusTone.Muted, "", null, null, null, null, false),
    };

    private static FocusAlertContent ForDistraction(Distraction d, string goal, FocusEnforcement enforcement)
    {
        var noun = d.IsBrowser ? "tab" : "window";
        var caption = $"{d.SecondsLeft}s";
        if (d.Nudged)
        {
            return new(WarningGlyph, d.Level > 0.25 ? FocusTone.Warning : FocusTone.Danger,
                "Halfway. Get back to work", $"Back to: {goal}", caption, d.Level, null, false);
        }
        return new(EyeGlyph, FocusTone.Warning, $"Drifting to {d.Label}",
            $"This {noun} is {enforcement.Fate()} when the bar empties", caption, d.Level, null, false);
    }
}
