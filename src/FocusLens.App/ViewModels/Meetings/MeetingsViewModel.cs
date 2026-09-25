using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLens.App.Services.Dialogs;
using FocusLens.Core.Meetings;
using FocusLens.Core.Repositories;

namespace FocusLens.App.ViewModels.Meetings;

/// <summary>Meetings list, open action items, and the selected meeting's detail.</summary>
public sealed partial class MeetingsViewModel : ObservableObject
{
    private readonly MeetingStore _store;
    private readonly MeetingSummarizer _summarizer;
    private readonly MeetingSessionCoordinator _coordinator;

    [ObservableProperty] private MeetingDetailViewModel? _detail;
    [ObservableProperty] private MeetingListItem? _selected;

    /// <summary>Record, pause and stop state, shared with the strip at the top of the window.</summary>
    public MeetingBannerViewModel Recorder { get; }

    public ObservableCollection<MeetingListItem> Meetings { get; } = new();
    public ObservableCollection<ActionItemWithMeeting> OpenActions { get; } = new();
    public bool IsEmpty => Meetings.Count == 0;

    public MeetingsViewModel(
        MeetingStore store, MeetingSummarizer summarizer, MeetingSessionCoordinator coordinator, MeetingBannerViewModel recorder)
    {
        Recorder = recorder;
        _store = store;
        _summarizer = summarizer;
        _coordinator = coordinator;
        Meetings.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
        _coordinator.Changed += s =>
        {
            if (s.Phase is MeetingPhase.Complete or MeetingPhase.Failed) UiThread.Post(() => _ = RefreshAsync());
        };
    }

    partial void OnSelectedChanged(MeetingListItem? value)
    {
        if (value is null) { Detail = null; return; }
        _ = OpenAsync(value.Id);
    }

    public async Task OpenAsync(string meetingId)
    {
        var detail = new MeetingDetailViewModel(meetingId, _store, _summarizer)
        {
            BackRequested = () => { Selected = null; Detail = null; },
            Deleted = () => { Selected = null; Detail = null; _ = RefreshAsync(); },
        };
        await detail.LoadAsync();
        Detail = detail;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var selectedId = Selected?.Id;
        Meetings.Clear();
        foreach (var meeting in await _store.RecentAsync()) Meetings.Add(MeetingListItem.From(meeting));
        Selected = Meetings.FirstOrDefault(m => m.Id == selectedId);

        OpenActions.Clear();
        foreach (var item in await _store.AllActionItemsAsync()) OpenActions.Add(item);
    }

    [RelayCommand]
    private async Task DeleteAsync(MeetingListItem item)
    {
        if (!ConfirmDialog.AskDeleteMeeting(item.Title)) return;
        var meeting = await _store.FetchAsync(item.Id);
        await _store.DeleteMeetingAsync(item.Id);
        MeetingAudioFiles.TryDelete(meeting?.AudioDir);
        if (Selected?.Id == item.Id) Selected = null;
        await RefreshAsync();
    }

    [RelayCommand]
    private void Open(MeetingListItem item) => Selected = item;
}
