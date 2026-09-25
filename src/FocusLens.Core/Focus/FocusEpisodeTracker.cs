namespace FocusLens.Core.Focus;

/// <summary>
/// Turns second-by-second observations into distraction episodes: one per stretch of time on
/// the same off-topic site or app.
/// </summary>
public sealed class FocusEpisodeTracker
{
    private readonly List<DistractionEpisode> _episodes = new();
    private DistractionEpisode? _current;

    public IReadOnlyList<DistractionEpisode> Episodes => _episodes;

    /// <summary>Call once a second. <paramref name="off"/> is the off-topic window, or null when the user is not off topic.</summary>
    public void Tick(FocusContext? off, double dt, DateTime now)
    {
        if (off is null)
        {
            FinishCurrent();
            return;
        }
        if (_current is not null && _current.Key == off.Key)
        {
            _current.Seconds += dt;
            return;
        }
        FinishCurrent();
        _current = new DistractionEpisode
        {
            Key = off.Key,
            Label = off.Label,
            Title = off.WindowTitle,
            StartedAt = now,
            Seconds = dt,
        };
    }

    public void MarkNudged()
    {
        if (_current is not null) _current.Nudged = true;
    }

    public void Mark(EpisodeOutcome outcome)
    {
        if (_current is not null) _current.Outcome = outcome;
    }

    /// <summary>Closes the open episode. A glance under two seconds that led to nothing is noise and is dropped.</summary>
    public void FinishCurrent()
    {
        if (_current is not { } done) return;
        _current = null;
        if (done.Seconds >= 2 || done.Nudged || done.Outcome != EpisodeOutcome.Returned) _episodes.Add(done);
    }

    /// <summary>Ends the session and returns every episode.</summary>
    public List<DistractionEpisode> Finish()
    {
        if (_current?.Outcome == EpisodeOutcome.Returned) _current.Outcome = EpisodeOutcome.SessionEnded;
        FinishCurrent();
        return _episodes.ToList();
    }
}
