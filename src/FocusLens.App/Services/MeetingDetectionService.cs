using System.Windows.Threading;
using FocusLens.Core.Ai;
using FocusLens.Core.Meetings;
using FocusLens.Core.Meetings.Detection;
using FocusLens.Platform.Windows.Audio;
using FocusLens.Platform.Windows.Shell;

namespace FocusLens.App.Services;

/// <summary>Polls the audio sessions every two seconds and proposes meetings to the coordinator.</summary>
public sealed class MeetingDetectionService : IDisposable
{
    private readonly MeetingDetector _detector;
    private readonly MeetingSessionCoordinator _coordinator;
    private readonly TrayIconService _tray;
    private readonly DispatcherTimer _timer;
    private bool _busy;

    public MeetingDetectionService(MeetingSessionCoordinator coordinator, TrayIconService tray, Func<AiSettings> settings)
    {
        _coordinator = coordinator;
        _tray = tray;

        _detector = new MeetingDetector(
            new AudioSessionMonitor(), new WindowsProcessInspector(), new WindowsBrowserInspector(),
            MeetingProviderRegistry.LoadBundled(),
            () =>
            {
                var s = settings();
                return new DetectionOptions(
                    s.MeetingDetectionEnabled, s.MeetingDetectionIncludeChatApps,
                    s.MeetingDetectionDenyList.Select(d => d.ToLowerInvariant()).ToHashSet());
            });

        _detector.CandidateFound += OnCandidate;
        _detector.SessionEnded += OnSessionEnded;
        _coordinator.Changed += OnCoordinatorChanged;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => Tick();
    }

    public void Start() => _timer.Start();

    /// <summary>Detection touches COM audio APIs, so run it off the UI thread and skip overlapping ticks.</summary>
    private void Tick()
    {
        if (_busy) return;
        _busy = true;
        _ = Task.Run(() =>
        {
            try { _detector.Tick(); }
            catch { /* a failed poll is retried on the next tick */ }
            finally { _busy = false; }
        });
    }

    private void OnCandidate(MeetingCandidate candidate, MatchConfidence confidence)
    {
        _coordinator.Propose(candidate);
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
            _tray.Notify(
                $"{candidate.Provider.DisplayName()} meeting detected",
                "Click to start taking notes.",
                () =>
                {
                    _detector.NoteSessionStarted();
                    _coordinator.Start(candidate);
                }));
    }

    private void OnSessionEnded()
    {
        if (_coordinator.Phase is MeetingPhase.Recording or MeetingPhase.Paused)
            _coordinator.Stop();
    }

    private void OnCoordinatorChanged(MeetingStatusSnapshot snapshot)
    {
        switch (snapshot.Phase)
        {
            case MeetingPhase.Recording when snapshot.Source == MeetingSource.Detected:
                _detector.NoteSessionStarted();
                break;
            case MeetingPhase.Idle or MeetingPhase.Complete or MeetingPhase.Failed:
                _detector.NoteSessionEnded();
                break;
        }
    }

    public void Dismiss(MeetingProvider provider)
    {
        _detector.NoteDismissal(provider);
        _coordinator.DismissCandidate();
    }

    public void Dispose() => _timer.Stop();
}
