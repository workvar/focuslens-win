namespace FocusLens.Core.Focus.Session;

/// <summary>Ends a session: builds the record, saves it, and puts every surface back.</summary>
public sealed partial class FocusSessionController
{
    private void Finish(bool completed)
    {
        if (Session is not { } ended) return;
        var now = DateTime.UtcNow;

        _score.CloseTimeline();
        var record = new FocusSessionRecord
        {
            Id = ended.Id,
            Goal = ended.Goal,
            StartedAt = ended.StartedAt,
            EndedAt = now,
            PlannedSeconds = (int)ended.Duration.TotalSeconds,
            Enforcement = ended.Enforcement,
            Completed = completed,
            FocusedSeconds = _score.FocusedSeconds,
            DistractedSeconds = _score.DistractedSeconds,
            NeutralSeconds = _score.NeutralSeconds,
            Score = _score.FinalScore,
            Episodes = _episodes.Finish(),
            Timeline = _score.Timeline.ToList(),
        };
        Store.Add(record);

        _ticker?.Dispose();
        _ticker = null;
        _activity.Stop();
        Session = null;
        _countdownTarget = null;
        BlockTarget = null;
        Distraction = null;
        _observation = FocusObservation.Neutral;
        _observedContext = null;
        _latestContext = null;
        _pendingJudgement = null;
        _recentlyClosed = null;
        LastRecord = record;
        UpdateTrayText();
        SetLive(100, 1);

        if (completed) Flash(new FocusAlert.Finished(ended.Goal), TimeSpan.FromSeconds(6));
        else SetAlert(null);
        Changed?.Invoke();

        Classifier.Reset();
    }
}
