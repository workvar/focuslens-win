using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLens.App.Services.Dialogs;
using FocusLens.Core.Focus;

namespace FocusLens.App.ViewModels.Focus;

/// <summary>
/// Insights for one saved session: score, time split, the score over time, what pulled the
/// user away, and every distraction in order.
/// </summary>
public sealed partial class FocusSessionDetailViewModel : ObservableObject
{
    private readonly FocusSessionStore _store;

    public FocusSessionDetailViewModel(FocusSessionRecord record, FocusSessionStore store)
    {
        Record = record;
        _store = store;
        Subtitle = $"{FocusFormat.Day(record.StartedAt)}, {FocusFormat.Duration(record.Duration())} of " +
                   $"{FocusFormat.Duration(record.PlannedSeconds)} planned, {record.Enforcement.Title().ToLowerInvariant()} mode";
        Tiles = BuildTiles(record);
        Insights = record.Insights();
        var top = record.TopDistractions();
        var longest = Math.Max(1, top.Select(t => t.Seconds).DefaultIfEmpty(1).Max());
        Breakdown = top.Take(6).Select(t => new FocusBreakdownRow(
            t.Label, $"{FocusFormat.Duration(t.Seconds)}, {FocusFormat.Plural(t.Count, "time")}", t.Seconds / longest)).ToList();
        Episodes = record.Episodes.Select(FocusEpisodeRow.From).ToList();
    }

    public Action? BackRequested { get; init; }
    public Action? Deleted { get; init; }

    public FocusSessionRecord Record { get; }
    public int Score => Record.Score;
    public string Goal => Record.Goal;
    public string Subtitle { get; }
    public IReadOnlyList<FocusStatTile> Tiles { get; }
    public IReadOnlyList<string> Insights { get; }
    public IReadOnlyList<FocusBreakdownRow> Breakdown { get; }
    public IReadOnlyList<FocusEpisodeRow> Episodes { get; }
    public bool HasEpisodes => Episodes.Count > 0;

    [RelayCommand]
    private void Back() => BackRequested?.Invoke();

    [RelayCommand]
    private void Delete()
    {
        if (!ConfirmDialog.Ask("Delete this session?", "Its score and insights will be removed. This cannot be undone.")) return;
        _store.Delete(Record.Id);
        Deleted?.Invoke();
    }

    private static List<FocusStatTile> BuildTiles(FocusSessionRecord r)
    {
        var block = r.Enforcement == FocusEnforcement.Block;
        return new()
        {
            new("Focus score", r.Score.ToString(), FocusTone.Accent),
            new("Focused", FocusFormat.Duration(r.FocusedSeconds), FocusTone.Success),
            new("Distracted", FocusFormat.Duration(r.DistractedSeconds), FocusTone.Danger),
            new("Distractions", r.DistractionCount().ToString(), FocusTone.Muted),
            new("Nudges", r.NudgeCount().ToString(), FocusTone.Muted),
            new(block ? "Blocked" : "Closed", (block ? r.BlockCount() : r.CloseCount()).ToString(), FocusTone.Muted),
        };
    }
}

/// <summary>One number with a label. Muted means the normal text colour here.</summary>
public sealed record FocusStatTile(string Title, string Value, FocusTone Tone);

/// <summary>A site or app and the share of the longest bar it fills.</summary>
public sealed record FocusBreakdownRow(string Label, string Detail, double Fraction);

/// <summary>One distraction with what happened next.</summary>
public sealed record FocusEpisodeRow(string Clock, string Label, string Title, string Duration, string Badge, FocusTone Tone)
{
    public bool HasTitle => Title.Length > 0;

    public static FocusEpisodeRow From(DistractionEpisode e)
    {
        var (badge, tone) = e.Outcome switch
        {
            EpisodeOutcome.Closed => ("Closed", FocusTone.Danger),
            EpisodeOutcome.Blocked => ("Blocked", FocusTone.Danger),
            EpisodeOutcome.CloseFailed => ("Could not close", FocusTone.Warning),
            EpisodeOutcome.Snoozed => ("Snoozed", FocusTone.Warning),
            EpisodeOutcome.SessionEnded => ("Session ended", FocusTone.Muted),
            _ => (e.Nudged ? "Nudged, then returned" : "Returned on own", FocusTone.Success),
        };
        return new(FocusFormat.Clock(e.StartedAt), e.Label, e.Title, FocusFormat.Duration(e.Seconds), badge, tone);
    }
}
