using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLens.App.Services;
using FocusLens.Core.Meetings;

namespace FocusLens.App.ViewModels.Meetings;

/// <summary>The always-visible strip that shows meeting state and offers Start, Pause, Resume and Stop.</summary>
public sealed partial class MeetingBannerViewModel : ObservableObject
{
    private readonly MeetingSessionCoordinator _coordinator;
    private readonly MeetingDetectionService _detection;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private MeetingStatusSnapshot _snapshot = MeetingStatusSnapshot.Idle;

    [ObservableProperty] private bool _isVisible;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _phaseText = "";
    [ObservableProperty] private string _elapsedText = "";
    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool _showProgress;
    [ObservableProperty] private bool _canStart;
    [ObservableProperty] private bool _canDismiss;
    [ObservableProperty] private bool _canPause;
    [ObservableProperty] private bool _canResume;
    [ObservableProperty] private bool _canStop;
    [ObservableProperty] private bool _canAcknowledge;

    public MeetingBannerViewModel(MeetingSessionCoordinator coordinator, MeetingDetectionService detection)
    {
        _coordinator = coordinator;
        _detection = detection;
        _coordinator.Changed += s => UiThread.Post(() => Apply(s));
        _clock.Tick += (_, _) => UpdateElapsed();
    }

    private void Apply(MeetingStatusSnapshot snapshot)
    {
        _snapshot = snapshot;
        var phase = snapshot.Phase;

        IsVisible = phase.IsActive;
        Title = snapshot.Title;
        PhaseText = phase switch
        {
            MeetingPhase.Detected d => $"{d.Candidate.Provider.DisplayName()} meeting detected",
            MeetingPhase.Recording => "Recording",
            MeetingPhase.Paused => "Paused",
            MeetingPhase.Transcribing => "Transcribing",
            MeetingPhase.Summarizing => "Summarizing",
            MeetingPhase.Complete => "Notes ready",
            MeetingPhase.Failed f => f.Error.UserMessage(),
            _ => "",
        };

        ShowProgress = phase is MeetingPhase.Transcribing;
        Progress = phase is MeetingPhase.Transcribing t ? t.Progress : 0;
        CanStart = phase is MeetingPhase.Detected;
        CanDismiss = phase is MeetingPhase.Detected;
        CanPause = phase is MeetingPhase.Recording;
        CanResume = phase is MeetingPhase.Paused;
        CanStop = phase is MeetingPhase.Recording or MeetingPhase.Paused;
        CanAcknowledge = phase.IsTerminal;

        if (phase.IsCapturing) _clock.Start(); else _clock.Stop();
        UpdateElapsed();
    }

    private void UpdateElapsed() =>
        ElapsedText = _snapshot.Phase is MeetingPhase.Recording or MeetingPhase.Paused
            ? MeetingClock.Format(MeetingClock.Elapsed(_snapshot))
            : "";

    [RelayCommand]
    private void Start()
    {
        if (_snapshot.Phase is MeetingPhase.Detected detected) _coordinator.Start(detected.Candidate);
    }

    [RelayCommand]
    private void Dismiss()
    {
        if (_snapshot.Phase is MeetingPhase.Detected detected) _detection.Dismiss(detected.Candidate.Provider);
    }

    [RelayCommand] private void Pause() => _coordinator.Pause();
    [RelayCommand] private void Resume() => _coordinator.Resume();
    [RelayCommand] private void Stop() => _coordinator.Stop();
    [RelayCommand] private void Acknowledge() => _coordinator.Acknowledge();
}
