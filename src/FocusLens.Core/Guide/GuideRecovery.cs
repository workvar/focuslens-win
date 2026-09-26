namespace FocusLens.Core.Guide;

/// <summary>
/// Whether a wobble is survivable. Pure, so the rule about when Guide gives up lives in one
/// readable place instead of being scattered through the run loop.
///
/// The old loop ended the guide on the first of: one failed model call, two plans in a row naming
/// a control that was not on screen, three ticks without a target, or one click that did not change
/// the screen. Each of those happens on a perfectly good model during an ordinary task, which is
/// why a guide would stop halfway with nothing obviously wrong.
///
/// The rule now is progress, not budget. A guide keeps going as long as it is still getting the
/// user through actions. It stops when several plans in a row produce nothing, when the same
/// control fails twice, or when it has simply been running too long.
/// </summary>
public sealed class GuideRecovery
{
    /// <summary>
    /// A browsing task legitimately costs a plan per screen: open the browser, focus the address
    /// bar, type, wait for the page, pick a result, scroll, click download. Sixteen used to run out
    /// halfway through that.
    /// </summary>
    public const int MaxPlans = 40;
    public const int MaxActions = 40;
    /// <summary>Plans in a row that moved the user through nothing. The real "we are stuck".</summary>
    public const int MaxPlansWithoutProgress = 6;
    /// <summary>Replies in a row naming controls that were not on screen.</summary>
    public const int MaxUngrounded = 4;
    /// <summary>Ticks without the target before the step is treated as missing.</summary>
    public const int MissingTicksBeforeReplan = 5;
    /// <summary>Times one step's control can go missing before Guide looks for another way.</summary>
    public const int MaxMisses = 4;
    /// <summary>Controls that turned out to do nothing before Guide concludes it is lost.</summary>
    public const int MaxStuckLabels = 3;

    /// <summary>However well it is going, one guide does not run past this.</summary>
    public static readonly TimeSpan WallClock = TimeSpan.FromMinutes(20);

    private readonly DateTime _startedAt;
    private readonly List<string> _stuckLabels = new();

    public GuideRecovery(DateTime? now = null) => _startedAt = now ?? DateTime.UtcNow;

    public int Plans { get; private set; }
    public int Actions { get; private set; }
    public int PlansSinceProgress { get; private set; }
    public int Ungrounded { get; private set; }
    public int Misses { get; private set; }
    public bool ReachedActionLimit => Actions >= MaxActions;

    /// <summary>Labels the model must not choose again, newest first.</summary>
    public IReadOnlyList<string> BannedLabels => _stuckLabels.AsEnumerable().Reverse().ToList();

    public enum VerdictKind { CarryOn, Retrying, GiveUp }

    /// <summary>CarryOn: keep going. Retrying: keep going, and say why the ghost paused. GiveUp: stop, and say this.</summary>
    public readonly record struct Verdict(VerdictKind Kind, string Message = "")
    {
        public static Verdict CarryOn { get; } = new(VerdictKind.CarryOn);
        public static Verdict Retry(string message) => new(VerdictKind.Retrying, message);
        public static Verdict Stop(string message) => new(VerdictKind.GiveUp, message);
        public bool IsGiveUp => Kind == VerdictKind.GiveUp;
    }

    public Verdict PlanStarted(DateTime? now = null)
    {
        if (Plans >= MaxPlans)
            return Verdict.Stop("This is taking more steps than I can follow. Ask again from here.");
        if (PlansSinceProgress >= MaxPlansWithoutProgress)
            return Verdict.Stop("I looked a few times and could not find a way forward from this screen.");
        if ((now ?? DateTime.UtcNow) - _startedAt > WallClock)
            return Verdict.Stop("We have been at this a while, so I stopped. Ask again to pick it up.");
        Plans++;
        PlansSinceProgress++;
        return Verdict.CarryOn;
    }

    /// <summary>The user actually did something. Everything that was going wrong is forgiven.</summary>
    public void ActionTaken()
    {
        Actions++;
        PlansSinceProgress = 0;
        Ungrounded = 0;
        Misses = 0;
    }

    /// <summary>The reply named only controls that are not on this screen.</summary>
    public Verdict PlanWasUngrounded()
    {
        Ungrounded++;
        return Ungrounded >= MaxUngrounded
            ? Verdict.Stop("The controls I was told to use are not on this screen, so I stopped.")
            : Verdict.Retry("Checking the screen again...");
    }

    /// <summary>A step's control never appeared.</summary>
    public Verdict TargetWasMissing(string label)
    {
        Misses++;
        return Misses >= MaxMisses
            ? Verdict.Stop($"I cannot find \"{label}\" on screen, and I could not find another way round.")
            : Verdict.Retry("Looking for another way...");
    }

    /// <summary>
    /// A control was used and the screen stayed exactly the same. Once is normal: a click can miss,
    /// or the app can be slow. Twice on the same control means that control is not the way.
    /// </summary>
    public Verdict ControlDidNothing(string label)
    {
        var key = GuideLabel.Normalise(label);
        if (_stuckLabels.Contains(key))
            return Verdict.Stop($"\"{label}\" does not seem to do anything here, so I stopped.");
        _stuckLabels.Add(key);
        return _stuckLabels.Count >= MaxStuckLabels
            ? Verdict.Stop("Nothing on this screen is moving the task forward, so I stopped.")
            : Verdict.Retry("That did not change anything. Looking again...");
    }
}
