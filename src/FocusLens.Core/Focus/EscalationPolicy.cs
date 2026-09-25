namespace FocusLens.Core.Focus;

/// <summary>
/// The patience bar as a pure state machine. Time is passed in, so the whole ladder can be
/// exercised without waiting on a clock.
///
///   Off topic   The bar drains: full to empty takes <see cref="Patience"/> seconds.
///   50 percent  A nudge, once per stretch of drifting.
///   Empty       The action fires (close or block).
///   On topic    The bar refills at half the speed it drained, so hopping back for a moment
///               does not wipe out a minute of drifting.
///
/// Only real off-topic time drains the bar. Unknown and neutral time never does.
/// </summary>
public sealed class EscalationPolicy
{
    public enum Event
    {
        None,
        Nudge,
        /// <summary>The bar is empty. Repeats while it stays empty, so the caller must guard against acting twice.</summary>
        Depleted,
    }

    private DateTime? _snoozedUntil;
    private DateTime? _lastAdvance;

    public EscalationPolicy(double patienceSeconds) => Patience = Math.Max(patienceSeconds, 1);

    public double Patience { get; }
    /// <summary>Refill speed relative to drain speed.</summary>
    public double RefillFactor { get; init; } = 0.5;
    public double Level { get; private set; } = 1;
    public bool Nudged { get; private set; }

    public int SecondsLeft => (int)Math.Ceiling(Math.Round(Level * Patience, 6));

    public Event Advance(bool offTopic, DateTime now)
    {
        var dt = Math.Clamp((now - (_lastAdvance ?? now)).TotalSeconds, 0, 5);
        _lastAdvance = now;

        if (_snoozedUntil is { } until)
        {
            if (now < until)
            {
                Level = 1;
                return Event.None;
            }
            _snoozedUntil = null;
        }

        if (!offTopic)
        {
            Level = Math.Min(1, Level + dt / Patience * RefillFactor);
            if (Level > 0.75) Nudged = false;
            return Event.None;
        }

        var before = Level;
        Level = Math.Max(0, Level - dt / Patience);

        if (!Nudged && before > 0.5 && Level <= 0.5)
        {
            Nudged = true;
            return Event.Nudge;
        }
        return Level <= 0 ? Event.Depleted : Event.None;
    }

    /// <summary>Refill after an action has been taken or called off.</summary>
    public void Reset()
    {
        Level = 1;
        Nudged = false;
    }

    /// <summary>The user asked for more time.</summary>
    public void Snooze(DateTime until)
    {
        Reset();
        _snoozedUntil = until;
    }
}
