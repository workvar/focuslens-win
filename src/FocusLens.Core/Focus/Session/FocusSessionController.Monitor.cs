namespace FocusLens.Core.Focus.Session;

/// <summary>
/// Records what the classifier says about the foreground window. It only observes: the
/// patience bar and the actions live in .Enforce and read the observation once a second.
/// </summary>
public sealed partial class FocusSessionController
{
    /// <summary>How long a new page must stay in front before the model is asked.</summary>
    public static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(2);

    /// <summary>How long a window just closed by Focus Mode is ignored while it goes away.</summary>
    public static readonly TimeSpan ClosingGrace = TimeSpan.FromSeconds(5);
    /// <summary>Apps may ask "save changes?" in a new window of the same process; the user gets time to answer.</summary>
    public static readonly TimeSpan SavePromptGrace = TimeSpan.FromSeconds(20);

    private async void MonitorTick(FocusContext? current)
    {
        if (Session is not { } session || _isClassifying) return;
        if (current is not null && _recentlyClosed is { } closed && IsClosing(current, closed.Target)
            && DateTime.UtcNow < closed.Until)
        {
            current = null;
        }
        if (current is null)
        {
            _observation = FocusObservation.Neutral;
            _observedContext = null;
            return;
        }
        // Already judged this exact page. The classifier caches too, but this skips even the lookup.
        if (_observedContext == current && _observation.Kind != FocusObservationKind.Neutral) return;
        // Only a different site or app resets the reading. A changed title on the same one
        // (scrolling, unread counts) keeps the last reading until the new one arrives.
        if (_observedContext is { } seen && !seen.IsSameTarget(current)) _observation = FocusObservation.Neutral;

        _isClassifying = true;
        try
        {
            await JudgeAsync(session, current);
        }
        finally
        {
            // Cleared on every path, so a second judgement cannot start while this one is finishing.
            _isClassifying = false;
        }
    }

    private async Task JudgeAsync(FocusSession session, FocusContext context)
    {
        var verdict = Classifier.QuickVerdict(session.Goal, context);
        var askedModel = verdict is null;

        if (verdict is null)
        {
            // Only the model can tell. Ask once the page has stayed in front for SettleTime,
            // so glances while switching tabs cost nothing.
            if (_pendingJudgement is not { } pending || pending.Context != context
                || DateTime.UtcNow - pending.Since < SettleTime)
            {
                if (_pendingJudgement?.Context != context) _pendingJudgement = (context, DateTime.UtcNow);
                RequestRead(SettleTime);
                return;
            }
            verdict = await Classifier.VerdictAsync(session.Goal, context);
        }

        _pendingJudgement = null;
        if (Session?.Id != session.Id) return;
        // The user may have moved on while the model was thinking, so look again after a model answer.
        if (askedModel) _latestContext = await SafeReadAsync();
        if (Session?.Id != session.Id || _latestContext is not { } now || !now.IsSameTarget(context)) return;

        _observedContext = context;
        _observation = verdict switch
        {
            FocusVerdict.OnTopic => FocusObservation.Focused,
            FocusVerdict.OffTopic => FocusObservation.OffOn(context),
            _ => FocusObservation.Neutral,
        };
    }

    private async Task<FocusContext?> SafeReadAsync(bool allowStale = true)
    {
        try { return await _reader.CurrentAsync(allowStale); }
        catch { return null; }
    }

    /// <summary>The window just closed, or (for apps, not browser tabs) a prompt from the same process.</summary>
    private static bool IsClosing(FocusContext current, FocusContext closed) =>
        current.Window == closed.Window || (!closed.IsBrowser && current.Pid == closed.Pid);
}
