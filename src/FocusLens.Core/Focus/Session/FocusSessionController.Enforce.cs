namespace FocusLens.Core.Focus.Session;

/// <summary>
/// Runs once a second: feeds the latest observation to the score, the episode log and the
/// patience bar, and carries out what the bar decides.
///
///   50 percent  nudge (the drawer gets louder, a sound plays)
///   empty       close mode: a short countdown, then the tab or window closes
///               block mode: an overlay covers the window until the user closes it
///
/// Every path that ends an action calls <c>_policy.Reset()</c>. Without it the bar would stay
/// empty and never fire again for the same page.
/// </summary>
public sealed partial class FocusSessionController
{
    // Meter

    private void AdvanceMeter(double dt, DateTime now)
    {
        var off = _observation.Kind == FocusObservationKind.Off ? _observation.Context : null;
        var sample = _observation.Kind switch
        {
            FocusObservationKind.Focused => FocusScoreTracker.Sample.Focused,
            FocusObservationKind.Off => FocusScoreTracker.Sample.Distracted,
            _ => FocusScoreTracker.Sample.Neutral,
        };

        _score.Record(sample, dt);
        _episodes.Tick(off, dt, now);

        switch (_policy.Advance(off is not null, now))
        {
            case EscalationPolicy.Event.Nudge:
                _episodes.MarkNudged();
                Nudged?.Invoke();
                break;
            case EscalationPolicy.Event.Depleted when off is not null:
                Act(off);
                break;
        }

        // Rounded so the bar is not redrawn for tiny changes.
        SetLive(_score.LiveScore, Math.Round(_policy.Level, 2));
        PublishDistraction(off);
    }

    private void PublishDistraction(FocusContext? off)
    {
        Distraction? value = null;
        if (off is not null && _countdownTarget is null && BlockTarget is null && _policy.Level < 0.95)
            value = new Distraction(off.Label, off.IsBrowser, Math.Round(_policy.Level, 2), _policy.Nudged, _policy.SecondsLeft);
        if (value == Distraction) return;
        Distraction = value;
        Changed?.Invoke();
    }

    // Act

    private void Act(FocusContext context)
    {
        if (_countdownTarget is not null || BlockTarget is not null || Session is not { } session) return;
        Distraction = null;
        if (session.Enforcement == FocusEnforcement.Close)
        {
            _countdownTarget = context;
            SetAlert(new FocusAlert.Countdown(context.Label, context.IsBrowser, Settings.Countdown));
        }
        else
        {
            _episodes.Mark(EpisodeOutcome.Blocked);
            BlockTarget = context;
            SetAlert(null);
        }
        Changed?.Invoke();
    }

    /// <summary>Runs with each reading (every second while a countdown is up). Ends it early if the user left.</summary>
    private async void AdvanceCountdown(FocusContext? current)
    {
        if (Alert is not FocusAlert.Countdown countdown || _countdownTarget is not { } target) return;

        // Called off if the user left, or the page they are on is now judged on topic.
        var stillThere = current is not null && current.IsSameTarget(target);
        if (!stillThere || _observation.Kind == FocusObservationKind.Focused)
        {
            _countdownTarget = null;
            SetAlert(null);
            _episodes.FinishCurrent();
            _observation = FocusObservation.Neutral;
            _policy.Reset();
            return;
        }
        if (countdown.SecondsLeft > 1)
        {
            SetAlert(countdown with { SecondsLeft = countdown.SecondsLeft - 1 });
            return;
        }

        _countdownTarget = null;
        var sessionId = Session?.Id;
        var outcome = await SafeCloseAsync(target);
        if (Session?.Id != sessionId) return;
        if (outcome == FocusCloseOutcome.Closed)
        {
            DidClose(target);
        }
        else
        {
            _episodes.Mark(EpisodeOutcome.CloseFailed);
            _policy.Reset();
            Flash(new FocusAlert.CouldNotClose(countdown.Label), TimeSpan.FromSeconds(6));
        }
    }

    /// <summary>The user pressed "Stay 5 min" during a countdown.</summary>
    public void SnoozeCountdown()
    {
        if (_countdownTarget is null) return;
        _countdownTarget = null;
        _episodes.Mark(EpisodeOutcome.Snoozed);
        _episodes.FinishCurrent();
        _policy.Snooze(DateTime.UtcNow.AddMinutes(5));
        SetAlert(null);
    }

    // Block overlay

    /// <summary>The overlay's button: close what is being blocked.</summary>
    public async void CloseBlockedTarget()
    {
        if (BlockTarget is not { } target) return;
        var outcome = await SafeCloseAsync(target);
        if (BlockTarget != target) return;
        if (outcome == FocusCloseOutcome.Closed)
        {
            BlockTarget = null;
            DidClose(target, keepOutcome: true);
        }
        else
        {
            Flash(new FocusAlert.CouldNotClose(target.Label), TimeSpan.FromSeconds(6));
        }
    }

    /// <summary>
    /// Runs with each reading (every second while the overlay is up). Lifts the overlay once the
    /// user has left the page, and keeps it on the window if the window is moved.
    /// </summary>
    private void VerifyBlockTarget(FocusContext? current)
    {
        if (BlockTarget is not { } target) return;
        // Lifted when the user leaves, or the page under it is judged on topic.
        if (current is null || !current.IsSameTarget(target) || _observation.Kind == FocusObservationKind.Focused)
        {
            BlockTarget = null;
            _episodes.FinishCurrent();
            _observation = FocusObservation.Neutral;
            _observedContext = null;
            _policy.Reset();
            Changed?.Invoke();
            return;
        }
        if (current.Bounds != target.Bounds)
        {
            BlockTarget = current;
            Changed?.Invoke();
        }
    }

    // Shared

    private void DidClose(FocusContext context, bool keepOutcome = false)
    {
        if (!keepOutcome) _episodes.Mark(EpisodeOutcome.Closed);
        _episodes.FinishCurrent();
        _observation = FocusObservation.Neutral;
        _observedContext = null;
        _policy.Reset();
        Flash(new FocusAlert.Closed(context.Label), TimeSpan.FromSeconds(3));
    }

    private async Task<FocusCloseOutcome> SafeCloseAsync(FocusContext target)
    {
        try { return await _closer.CloseAsync(target); }
        catch { return FocusCloseOutcome.Failed; }
    }
}
