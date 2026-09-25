using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLens.Core.Meetings;
using FocusLens.Core.Repositories;

namespace FocusLens.App.ViewModels.Meetings;

public sealed partial class NoteBlockViewModel : ObservableObject
{
    public string Id { get; }
    public string Heading { get; }
    [ObservableProperty] private string _content;
    [ObservableProperty] private bool _isEdited;
    [ObservableProperty] private bool _isEditing;

    public NoteBlockViewModel(MeetingNote note)
    {
        Id = note.Id;
        Heading = NoteBlockTypeExtensions.FromId(note.BlockType).Heading();
        _content = note.ContentMd;
        _isEdited = note.EditedAt is not null;
    }
}

public sealed partial class ActionItemViewModel : ObservableObject
{
    public string Id { get; }
    public string Text { get; }
    public string Owner { get; }
    [ObservableProperty] private bool _isDone;

    public ActionItemViewModel(ActionItem item)
    {
        Id = item.Id;
        Text = item.Text;
        Owner = item.Owner ?? "";
        _isDone = item.Status == "done";
    }
}

public sealed record TranscriptLine(string Speaker, string Time, string Text);

/// <summary>One meeting's notes, action items and transcript, with editing and re-summarising.</summary>
public sealed partial class MeetingDetailViewModel : ObservableObject
{
    private readonly MeetingStore _store;
    private readonly MeetingSummarizer _summarizer;

    public string MeetingId { get; }
    public Action? BackRequested { get; set; }
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _status;

    public ObservableCollection<NoteBlockViewModel> Notes { get; } = new();
    public ObservableCollection<ActionItemViewModel> Actions { get; } = new();
    public ObservableCollection<TranscriptLine> Transcript { get; } = new();

    public MeetingDetailViewModel(string meetingId, MeetingStore store, MeetingSummarizer summarizer)
    {
        MeetingId = meetingId;
        _store = store;
        _summarizer = summarizer;
    }

    public async Task LoadAsync()
    {
        var meeting = await _store.FetchAsync(MeetingId);
        if (meeting is null) return;
        Title = meeting.Title;
        var started = DateTimeOffset.FromUnixTimeSeconds(meeting.StartedAt).ToLocalTime();
        Subtitle = $"{MeetingProviderExtensions.FromId(meeting.Provider).DisplayName()}, {started:ddd MMM d, h:mm tt}";

        Notes.Clear();
        foreach (var note in await _store.NotesAsync(MeetingId)) Notes.Add(new NoteBlockViewModel(note));

        Actions.Clear();
        foreach (var item in await _store.ActionItemsAsync(MeetingId)) Actions.Add(new ActionItemViewModel(item));

        Transcript.Clear();
        foreach (var segment in await _store.SegmentsAsync(MeetingId))
        {
            var track = TranscriptTrackExtensions.FromId(segment.Track);
            Transcript.Add(new TranscriptLine(track.SpeakerLabel(), MeetingClock.Format(TimeSpan.FromMilliseconds(segment.StartMs)), segment.Text));
        }
    }

    [RelayCommand]
    private void Back() => BackRequested?.Invoke();

    [RelayCommand]
    private async Task ToggleActionAsync(ActionItemViewModel item)
    {
        var done = !item.IsDone;
        item.IsDone = done;
        await _store.SetActionItemStatusAsync(item.Id, done ? ActionStatus.Done : ActionStatus.Open);
    }

    [RelayCommand]
    private void Edit(NoteBlockViewModel note) => note.IsEditing = true;

    [RelayCommand]
    private async Task SaveNoteAsync(NoteBlockViewModel note)
    {
        await _store.UpdateNoteAsync(note.Id, note.Content);
        note.IsEditing = false;
        note.IsEdited = true;
    }

    [RelayCommand]
    private async Task ResummarizeAsync()
    {
        IsBusy = true;
        Status = "Summarizing...";
        try
        {
            await _summarizer.SummarizeAsync(MeetingId, Title);
            await LoadAsync();
            Status = null;
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
