using FocusLens.Core.Guide;
using FocusLens.Platform.Windows.Guide;

namespace FocusLens.App.Services.Guide;

/// <summary>
/// Runs one guided task. Runs on the UI thread; every await resumes there.
///
/// Shape of a run:
///   1. Read the screen and the list of open apps.
///   2. Ask for a route once: the goal and a few stages, in plain words. This is the pre-planning,
///      and it is what stops a replan from restarting the task.
///   3. Loop: plan the next one to three actions against the live screen, keep only the ones whose
///      labels are really there, and walk the user through them. After an action that navigates,
///      wait for the screen to settle before planning again.
///
/// Nothing in that loop ends the guide on its own. A failed model call is retried, an unreadable
/// reply is asked again, a control that is not there becomes a question about another way round,
/// and a control that does nothing is banned rather than fatal. GuideRecovery holds the one rule
/// that does stop a run: several plans in a row with no progress.
/// </summary>
public sealed class GuideSessionController
{
    private readonly IGuidePlanner _planner;
    private readonly IGuideRouter? _router;
    private readonly UiaWalker _walker;
    private readonly GuideCursorController _cursor;
    private readonly GuideStepRunner _runner;

    private CancellationTokenSource? _cts;
    private GuideInputHook? _hook;
    private DateTime _lastEscape = DateTime.MinValue;

    public GuidePhase Phase { get; private set; } = GuidePhase.Idle;
    public event Action? PhaseChanged;
    public bool IsActive => Phase.IsRunning;

    /// <summary>The text the current step asks the user to type, if it is a typing step.</summary>
    public string? TypingText => _runner.TypingText;

    public GuideSessionController(IGuidePlanner planner, UiaWalker walker, GuideCursorController cursor,
        IGuideRouter? router = null)
    {
        _planner = planner;
        _router = router;
        _walker = walker;
        _cursor = cursor;
        _runner = new GuideStepRunner(walker, cursor);
    }

    // MARK: Control

    public void Begin(string request, IntPtr workingWindow)
    {
        Stop();
        request = request.Trim();
        if (request.Length == 0) return;
        _walker.PreferredWindow = workingWindow;
        _cts = new CancellationTokenSource();
        _ = RunAsync(request, _cts.Token);
    }

    public void Skip() => _runner.Skip();

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        EndInput();
        _cursor.Hide();
        SetPhase(GuidePhase.Idle);
    }

    // MARK: Run

    private async Task RunAsync(string request, CancellationToken ct)
    {
        try
        {
            StartInput();
            _cursor.Show();
            SetPhase(new GuidePhase(GuidePhaseKind.Planning));

            _cursor.Follow("Looking at this computer...");
            var context = new GuidePromptContext
            {
                Request = request,
                System = SystemInfo.Read(),
                Apps = OpenApps.List(),
                Notes = await _planner.NotesAsync(request, ct),
                Screen = await _walker.SnapshotAsync(),
            };
            context = context with { Route = await MakeRouteAsync(context, ct) };

            var recovery = new GuideRecovery();
            var done = new List<GuideStep>();
            var ahead = new List<GuideStep>();
            // The first step of a fresh plan is allowed a moment to appear. Later steps were named
            // against an older screen, so a missing control means replan now.
            var fresh = true;

            while (!recovery.ReachedActionLimit && !ct.IsCancellationRequested)
            {
                if (ahead.Count == 0)
                {
                    var verdict = recovery.PlanStarted();
                    if (verdict.IsGiveUp) { await FailAsync(verdict.Message, ct); return; }
                    _cursor.Follow(verdict.Kind == GuideRecovery.VerdictKind.Retrying
                        ? verdict.Message
                        : recovery.Plans == 1 ? "Looking at the screen..." : "Checking the screen...");
                    SetPhase(new GuidePhase(GuidePhaseKind.Planning));

                    context = context with
                    {
                        Apps = OpenApps.List(),
                        Screen = await _walker.SnapshotAsync(),
                        Done = done.ToList(),
                        BannedLabels = recovery.BannedLabels,
                    };

                    GuidePlan plan;
                    try { plan = await _planner.PlanAsync(context, ct); }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        await FailAsync(GuideRetry.Message(ex), ct);
                        return;
                    }
                    context = context with { Missed = null };
                    fresh = true;

                    if (plan.Status == GuidePlanStatus.Done)
                    {
                        await FinishAsync(plan.Note ?? plan.Steps.FirstOrDefault()?.Title, ct);
                        return;
                    }
                    if (plan.Status == GuidePlanStatus.Blocked)
                    {
                        await FailAsync(plan.Note ?? "I can't go further from this screen.", ct);
                        return;
                    }

                    var grounded = GuideElementMatcher.Grounded(plan.Steps, context.Screen);
                    if (grounded.Count == 0)
                    {
                        context = context with { RejectedLabels = GuideElementMatcher.InventedLabels(plan.Steps) };
                        var ungrounded = recovery.PlanWasUngrounded();
                        if (ungrounded.IsGiveUp) { await FailAsync(ungrounded.Message, ct); return; }
                        continue;
                    }
                    context = context with { RejectedLabels = Array.Empty<string>() };
                    ahead.AddRange(grounded);
                }

                var step = ahead[0];
                ahead.RemoveAt(0);
                var visible = await _walker.SnapshotAsync();
                if (!fresh && !GuideElementMatcher.StillOnScreen(step, visible))
                {
                    ahead.Clear();
                    continue;
                }
                fresh = false;
                var before = GuideElementMatcher.Fingerprint(visible);
                var inBrowser = context.Apps.Any(a => a.IsFront && a.IsBrowser);

                SetPhase(new GuidePhase(GuidePhaseKind.Guiding, done.Count, done.Count + 1 + ahead.Count));
                var outcome = await _runner.RunAsync(step, ct);
                if (outcome == GuideStepRunner.Outcome.Cancelled) return;

                if (outcome is GuideStepRunner.Outcome.Completed or GuideStepRunner.Outcome.Skipped)
                {
                    done.Add(step);
                    recovery.ActionTaken();
                    // A step that replaces the screen (a page load, an app coming forward) needs a
                    // moment before the next plan, or the model is shown a half-drawn window and
                    // told nothing is there.
                    if (GuideSettle.Navigates(step, inBrowser))
                    {
                        ahead.Clear();
                        await WaitForSettleAsync(ct);
                    }
                    else if (ChangesTheScreen(step) && step.Target?.Label is { } used)
                    {
                        var after = await _walker.SnapshotAsync();
                        if (GuideElementMatcher.Fingerprint(after) == before)
                        {
                            ahead.Clear();
                            var stuck = recovery.ControlDidNothing(used);
                            if (stuck.IsGiveUp) { await FailAsync(stuck.Message, ct); return; }
                        }
                    }
                    continue;
                }

                // NotFound.
                context = context with { Missed = step };
                ahead.Clear();
                var missing = recovery.TargetWasMissing(step.Target?.Label ?? step.Title);
                if (missing.IsGiveUp) { await FailAsync(missing.Message, ct); return; }
            }

            if (!ct.IsCancellationRequested)
                await FinishAsync("That is as far as I can take it in one go. Ask again to carry on.", ct);
        }
        catch (OperationCanceledException)
        {
            // Stop() already cleaned up.
        }
        catch (Exception)
        {
            // Anything unexpected must still end the guide; otherwise the ghost stays on "Thinking...".
            if (!ct.IsCancellationRequested)
                await FailAsync("Something went wrong. Try asking again.", CancellationToken.None);
        }
    }

    // MARK: Pieces of a run

    /// <summary>
    /// The route is a nicety, so it is never allowed to fail a run: no route just means the guide
    /// steers one screen at a time, as it did before.
    /// </summary>
    private async Task<GuideRoute?> MakeRouteAsync(GuidePromptContext context, CancellationToken ct)
    {
        if (_router is null) return null;
        _cursor.Follow("Working out how to do this...");
        var route = await _router.RouteAsync(context.Request, context.System, context.Apps, context.Screen, ct);
        if (route is { Goal.Length: > 0 }) _cursor.Follow(route.Goal);
        return route;
    }

    /// <summary>
    /// Waits for the screen to stop changing after a navigating step. Bounded, so a page that never
    /// finishes loading costs seconds, not the guide.
    /// </summary>
    private async Task WaitForSettleAsync(CancellationToken ct)
    {
        _cursor.Follow("Waiting for that to load...");
        await Task.Delay(GuideSettle.LeadIn, ct);
        var previous = GuideElementMatcher.Fingerprint(await _walker.SnapshotAsync());
        var deadline = DateTime.UtcNow + GuideSettle.MaxWait;
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            await Task.Delay(GuideSettle.Quiet, ct);
            var current = GuideElementMatcher.Fingerprint(await _walker.SnapshotAsync());
            if (current == previous) return;
            previous = current;
        }
    }

    /// <summary>
    /// Clicks, toggles, and opens are supposed to change the screen. Typing, reading, and focusing
    /// the address field often do not, so those are not treated as a stuck control.
    /// </summary>
    private static bool ChangesTheScreen(GuideStep step) =>
        !string.Equals(step.Target?.Role, "field", StringComparison.OrdinalIgnoreCase) &&
        step.Action is GuideAction.Click or GuideAction.Toggle or GuideAction.Open;

    // MARK: Input

    private void StartInput()
    {
        _hook = new GuideInputHook();
        _hook.Click += (x, y) => _runner.Post(new GuideStepEvent(GuideStepEventKind.Click, x, y));
        _hook.ReturnKey += () => _runner.Post(new GuideStepEvent(GuideStepEventKind.ReturnKey));
        // Esc closes menus and dialogs in the app being guided, so stopping needs a deliberate double tap.
        _hook.EscapeKey += () =>
        {
            var now = DateTime.UtcNow;
            if (now - _lastEscape < TimeSpan.FromMilliseconds(600)) { _lastEscape = DateTime.MinValue; Stop(); }
            else _lastEscape = now;
        };
        _hook.Start();
    }

    private void EndInput()
    {
        _hook?.Dispose();
        _hook = null;
        _runner.End();
    }

    // MARK: Endings

    private async Task FinishAsync(string? note, CancellationToken ct)
    {
        EndInput();
        var trimmed = note?.Trim();
        _cursor.Celebrate(string.IsNullOrEmpty(trimmed) ? "All done" : trimmed);
        SetPhase(new GuidePhase(GuidePhaseKind.Finished));
        await Task.Delay(2500, ct);
        _cursor.Hide();
        SetPhase(GuidePhase.Idle);
    }

    private async Task FailAsync(string message, CancellationToken ct)
    {
        EndInput();
        SetPhase(new GuidePhase(GuidePhaseKind.Failed, Message: message));
        _cursor.Follow(message);
        await Task.Delay(4000, ct);
        _cursor.Hide();
        SetPhase(GuidePhase.Idle);
    }

    private void SetPhase(GuidePhase phase)
    {
        Phase = phase;
        PhaseChanged?.Invoke();
    }
}
