using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLens.Core.Focus;
using FocusLens.Core.Focus.Session;

namespace FocusLens.App.ViewModels.Focus;

/// <summary>
/// The Focus page: set a goal, a length and what happens when you drift; watch the clock; look
/// back at past sessions. A session's insights open in place, like a meeting's detail.
/// </summary>
public sealed partial class FocusViewModel : ObservableObject
{
    private readonly FocusSessionController _controller;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private string _goal = "";
    [ObservableProperty] private FocusSessionItem? _lastSession;
    [ObservableProperty] private FocusSessionDetailViewModel? _detail;

    public FocusViewModel(FocusSessionController controller, FocusLiveViewModel live, FocusSettingsViewModel settings)
    {
        _controller = controller;
        Live = live;
        Settings = settings;
        Durations = FocusDurationOption.All();
        Durations.First(d => d.Minutes == 25).IsSelected = true;
        // Remembers the last choice, so the start screen opens the way it was left.
        Enforcements = new() { new(FocusEnforcement.Close), new(FocusEnforcement.Block) };
        Enforcements.First(e => e.Mode == controller.Settings.Enforcement).IsSelected = true;

        controller.Changed += () => UiThread.Post(SyncLastSession);
        controller.Store.Changed += () => UiThread.Post(ReloadSessions);
        ReloadSessions();
    }

    public FocusLiveViewModel Live { get; }
    public FocusSettingsViewModel Settings { get; }
    public IReadOnlyList<FocusDurationOption> Durations { get; }
    public List<FocusEnforcementOption> Enforcements { get; }
    public ObservableCollection<FocusSessionItem> Sessions { get; } = new();
    public bool HasSessions => Sessions.Count > 0;

    private bool CanStart() => Goal.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        var minutes = Durations.First(d => d.IsSelected).Minutes;
        var mode = Enforcements.First(e => e.IsSelected).Mode;
        _controller.Start(Goal, minutes, mode);
        Goal = "";
    }

    [RelayCommand]
    private void Stop() => _controller.Stop();

    [RelayCommand]
    private void SelectDuration(FocusDurationOption option)
    {
        foreach (var d in Durations) d.IsSelected = d == option;
    }

    [RelayCommand]
    private void SelectEnforcement(FocusEnforcementOption option)
    {
        foreach (var e in Enforcements) e.IsSelected = e == option;
        _controller.Settings.Enforcement = option.Mode;
        _controller.Settings.Save();
    }

    [RelayCommand]
    private void Open(FocusSessionItem item)
    {
        Detail = new FocusSessionDetailViewModel(item.Record, _controller.Store)
        {
            BackRequested = () => Detail = null,
            Deleted = () => Detail = null,
        };
    }

    /// <summary>Leaves a session's insights when the Focus row in the sidebar is clicked again.</summary>
    public void ShowOverview() => Detail = null;

    /// <summary>Changed fires every second during a session, so the card is rebuilt only for a new record.</summary>
    private void SyncLastSession()
    {
        var record = _controller.LastRecord;
        // Deleted from its insights page: do not bring the card back.
        if (record is not null && _controller.Store.Find(record.Id) is null) record = null;
        if (record?.Id == LastSession?.Record.Id) return;
        LastSession = record is null ? null : new FocusSessionItem(record);
    }

    private void ReloadSessions()
    {
        Sessions.Clear();
        foreach (var record in _controller.Store.Sessions) Sessions.Add(new FocusSessionItem(record));
        if (LastSession is { } last && _controller.Store.Find(last.Record.Id) is null) LastSession = null;
        OnPropertyChanged(nameof(HasSessions));
    }
}
