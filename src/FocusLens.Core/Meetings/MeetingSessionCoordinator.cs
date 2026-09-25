namespace FocusLens.Core.Meetings;

/// <summary>
/// Owns the meeting note-taking state machine: idle, detected, recording, paused,
/// transcribing, summarising, complete or failed. Status surfaces subscribe via
/// <see cref="Changed"/> or attach an <see cref="IMeetingStatusSink"/>. Events fire on the
/// calling thread; UI code must marshal to its dispatcher.
/// </summary>
public sealed class MeetingSessionCoordinator
{
    private readonly object _gate = new();
    private readonly IMeetingProcessor _processor;
    private readonly Func<DateTime> _now;
    private readonly TimeSpan _lingerDuration;
    private readonly List<IMeetingStatusSink> _sinks = new();

    private IMeetingRecorder? _recorder;
    private string? _activeMeetingId;
    private CancellationTokenSource? _processingCts;
    private CancellationTokenSource? _lingerCts;
    private CancellationTokenSource? _candidateCts;

    public MeetingStatusSnapshot Snapshot { get; private set; } = MeetingStatusSnapshot.Idle;
    public MeetingPhase Phase => Snapshot.Phase;
    public event Action<MeetingStatusSnapshot>? Changed;

    public MeetingSessionCoordinator(
        IMeetingProcessor? processor = null, Func<DateTime>? now = null, TimeSpan? lingerDuration = null)
    {
        _processor = processor ?? new NoopMeetingProcessor();
        _now = now ?? (() => DateTime.UtcNow);
        _lingerDuration = lingerDuration ?? TimeSpan.FromSeconds(30);
    }

    public void Attach(IMeetingStatusSink sink)
    {
        _sinks.Add(sink);
        sink.Receive(Snapshot);
    }

    public void Attach(IMeetingRecorder recorder) => _recorder = recorder;

    // Detection

    public void Propose(MeetingCandidate candidate, TimeSpan? timeout = null)
    {
        lock (_gate)
        {
            if (Phase is not MeetingPhase.Idle) return;
            Apply(MeetingStatusSnapshot.Idle with
            {
                Phase = new MeetingPhase.Detected(candidate),
                Title = candidate.ResolvedTitle,
                Provider = candidate.Provider,
                Source = candidate.Source,
            });
        }
        Restart(ref _candidateCts, timeout ?? TimeSpan.FromSeconds(90), () =>
        {
            lock (_gate) { if (Phase is MeetingPhase.Detected) Apply(MeetingStatusSnapshot.Idle); }
        });
    }

    public void DismissCandidate()
    {
        lock (_gate)
        {
            if (Phase is not MeetingPhase.Detected) return;
            Cancel(ref _candidateCts);
            Apply(MeetingStatusSnapshot.Idle);
        }
    }

    // Recording

    public string? Start(MeetingCandidate? candidate = null)
    {
        candidate ??= new MeetingCandidate();
        string id;
        DateTime startedAt;
        lock (_gate)
        {
            if (Phase is not (MeetingPhase.Idle or MeetingPhase.Detected)) return null;
            Cancel(ref _candidateCts);
            Cancel(ref _lingerCts);

            id = Guid.NewGuid().ToString();
            _activeMeetingId = id;
            startedAt = _now();
            Apply(MeetingStatusSnapshot.Idle with
            {
                Phase = new MeetingPhase.Recording(),
                MeetingId = id,
                Title = candidate.ResolvedTitle,
                Provider = candidate.Provider,
                Source = candidate.Source,
                StartedAt = startedAt,
            });
        }

        if (_recorder is { } recorder)
        {
            _ = Task.Run(async () =>
            {
                try { await recorder.BeginAsync(id, candidate, startedAt); }
                catch (Exception ex) { Fail(ErrorFrom(ex)); }
            });
        }
        return id;
    }

    public void Pause()
    {
        lock (_gate)
        {
            if (Phase is not MeetingPhase.Recording) return;
            Apply(Snapshot with { Phase = new MeetingPhase.Paused(), PausedSince = _now() });
        }
        _recorder?.Pause();
    }

    public void Resume()
    {
        lock (_gate)
        {
            if (Phase is not MeetingPhase.Paused || Snapshot.PausedSince is not { } since) return;
            Apply(Snapshot with
            {
                Phase = new MeetingPhase.Recording(),
                PausedTotal = Snapshot.PausedTotal + (_now() - since),
                PausedSince = null,
            });
        }
        _recorder?.Resume();
    }

    public void Stop()
    {
        string id;
        int duration;
        lock (_gate)
        {
            if (Phase is not (MeetingPhase.Recording or MeetingPhase.Paused) || _activeMeetingId is null) return;
            id = _activeMeetingId;

            var next = Snapshot;
            if (next.PausedSince is { } since)
                next = next with { PausedTotal = next.PausedTotal + (_now() - since), PausedSince = null };
            next = next with { Phase = new MeetingPhase.Transcribing(0) };
            Apply(next);
            duration = (int)MeetingClock.Elapsed(next, _now()).TotalSeconds;
        }

        if (_recorder is { } recorder)
        {
            _ = Task.Run(async () =>
            {
                await recorder.EndAsync(duration);
                RunProcessing(id);
            });
        }
        else
        {
            RunProcessing(id);
        }
    }

    public void Acknowledge()
    {
        lock (_gate)
        {
            if (!Phase.IsTerminal) return;
            Cancel(ref _lingerCts);
            Apply(MeetingStatusSnapshot.Idle);
        }
    }

    public void Fail(MeetingError error)
    {
        lock (_gate)
        {
            Cancel(ref _processingCts);
            Apply(Snapshot with { Phase = new MeetingPhase.Failed(error) });
        }
        ScheduleLinger();
    }

    // Processing

    private void RunProcessing(string meetingId)
    {
        CancellationTokenSource cts;
        lock (_gate)
        {
            Cancel(ref _processingCts);
            cts = _processingCts = new CancellationTokenSource();
        }

        _ = Task.Run(async () =>
        {
            var sawTerminal = false;
            try
            {
                await foreach (var evt in _processor.ProcessAsync(meetingId, cts.Token))
                {
                    switch (evt)
                    {
                        case MeetingProcessingEvent.Transcribing t:
                            SetPhase(new MeetingPhase.Transcribing(Math.Clamp(t.Progress, 0, 1)));
                            break;
                        case MeetingProcessingEvent.Summarizing:
                            SetPhase(new MeetingPhase.Summarizing());
                            break;
                        case MeetingProcessingEvent.Finished:
                            sawTerminal = true;
                            FinishSession(meetingId);
                            break;
                        case MeetingProcessingEvent.Failed f:
                            sawTerminal = true;
                            Fail(f.Error);
                            break;
                    }
                }
            }
            catch (OperationCanceledException) { return; }
            catch { if (!sawTerminal) { sawTerminal = true; Fail(MeetingError.TranscriptionFailed); } }

            if (!sawTerminal && !cts.IsCancellationRequested && Phase.IsProcessing)
                Fail(MeetingError.TranscriptionFailed);
        });
    }

    private void FinishSession(string meetingId)
    {
        lock (_gate)
        {
            Apply(Snapshot with { Phase = new MeetingPhase.Complete(meetingId) });
            _activeMeetingId = null;
        }
        ScheduleLinger();
    }

    private void ScheduleLinger() => Restart(ref _lingerCts, _lingerDuration, () =>
    {
        lock (_gate) { if (Phase.IsTerminal) Apply(MeetingStatusSnapshot.Idle); }
    });

    private void SetPhase(MeetingPhase phase)
    {
        lock (_gate) Apply(Snapshot with { Phase = phase });
    }

    private void Apply(MeetingStatusSnapshot next)
    {
        if (next == Snapshot) return;
        Snapshot = next;
        foreach (var sink in _sinks) sink.Receive(next);
        Changed?.Invoke(next);
    }

    private static MeetingError ErrorFrom(Exception ex) => ex switch
    {
        MeetingCaptureException capture => capture.Error,
        TranscriptionException { Failure: TranscriptionFailure.NotAuthorized } => MeetingError.MicrophoneDenied,
        TranscriptionException => MeetingError.TranscriptionFailed,
        _ => MeetingError.AudioCaptureFailed,
    };

    // Timer helpers

    private void Restart(ref CancellationTokenSource? slot, TimeSpan delay, Action action)
    {
        Cancel(ref slot);
        var cts = slot = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(delay, cts.Token); }
            catch (OperationCanceledException) { return; }
            action();
        });
    }

    private static void Cancel(ref CancellationTokenSource? slot)
    {
        slot?.Cancel();
        slot = null;
    }
}
