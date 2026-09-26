using System.Threading.Channels;
using FocusLens.Core.Guide;
using FocusLens.Platform.Windows.Guide;

namespace FocusLens.App.Services.Guide;

/// <summary>
/// Runs one guided task. The model plans only the next few actions from the screen in front of the
/// user. After each action, if the following step's control is still visible, Guide continues at
/// once. If the screen has moved on (a menu opened, a page changed, a dialog appeared), the rest of
/// that plan is dropped and the model is shown the new screen. Runs on the UI thread; every await
/// resumes there.
///
/// Every step is resolved against the live screen when it starts and re-checked once a second
/// while it waits, so the cursor follows a window the user drags and notices when the target has
/// gone. A step ends when the user clicks its target, presses Return (typing steps), or asks to
/// skip. A label the model invented is never shown. A click that leaves the screen unchanged
/// is not repeated.
/// </summary>
public sealed class GuideSessionController
{
    private enum Outcome { Completed, Skipped, NotFound, Cancelled }

    private const int MissingTicksBeforeReplan = 5;
    private const int MaxPlans = 16;
    private const int MaxActions = 18;
    private const int MaxMisses = 3;

    private readonly IGuidePlanner _planner;
    private readonly UiaWalker _walker;
    private readonly GuideCursorController _cursor;

    private CancellationTokenSource? _cts;
    private Channel<GuideStepEvent>? _events;
    private GuideInputHook? _hook;
    private DateTime _lastEscape = DateTime.MinValue;

    public GuidePhase Phase { get; private set; } = GuidePhase.Idle;
    public event Action? PhaseChanged;
    public bool IsActive => Phase.IsRunning;

    /// <summary>The text the current step asks the user to type, if it is a typing step. Hold to fill offers it.</summary>
    public string? TypingText { get; private set; }

    public GuideSessionController(IGuidePlanner planner, UiaWalker walker, GuideCursorController cursor)
    {
        _planner = planner;
        _walker = walker;
        _cursor = cursor;
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

    public void Skip() => _events?.Writer.TryWrite(new GuideStepEvent(GuideStepEventKind.Skip));

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

            var done = new List<GuideStep>();
            var ahead = new List<GuideStep>();
            GuideStep? missed = null;
            var plans = 0;
            var actions = 0;
            var misses = 0;
            var ungrounded = 0;
            // Labels the model just invented. Passed back so the next reply cannot reuse them.
            IReadOnlyList<string> rejected = Array.Empty<string>();
            // A control the user already used when that click left the screen the same, and the screen
            // as it was then. A later snapshot that differs means the click did land, so that label may
            // be used again.
            string? stuckLabel = null;
            string? stuckFingerprint = null;
            // The first step of a fresh plan is allowed a moment to appear. Later steps were named
            // against an older screen, so a missing control means replan now.
            var fresh = true;

            while (actions < MaxActions && !ct.IsCancellationRequested)
            {
                if (ahead.Count == 0)
                {
                    if (plans >= MaxPlans)
                    {
                        await FailAsync("The screen kept changing, so I stopped. Ask again from here.", ct);
                        return;
                    }
                    _cursor.Follow(plans == 0 ? "Looking at the screen..." : "Checking the screen...");
                    SetPhase(new GuidePhase(GuidePhaseKind.Planning));
                    var screen = await _walker.SnapshotAsync();
                    if (stuckFingerprint is { } stuckKey && GuideElementMatcher.Fingerprint(screen) != stuckKey)
                    {
                        stuckLabel = null;
                        stuckFingerprint = null;
                    }
                    GuidePlan plan;
                    try { plan = await _planner.PlanAsync(request, done, missed, screen, stuckLabel, rejected, ct); }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        await FailAsync("I could not plan that. Try asking again.", ct);
                        return;
                    }
                    plans++;
                    missed = null;
                    fresh = true;
                    switch (plan.Status)
                    {
                        case GuidePlanStatus.Done:
                            await FinishAsync(plan.Note ?? plan.Steps.FirstOrDefault()?.Title, ct);
                            return;
                        case GuidePlanStatus.Blocked:
                            await FailAsync(plan.Note ?? "I can't go further from this screen.", ct);
                            return;
                        default:
                            var grounded = GuideElementMatcher.Grounded(plan.Steps, screen);
                            if (grounded.Count == 0)
                            {
                                rejected = GuideElementMatcher.InventedLabels(plan.Steps);
                                ungrounded++;
                                if (ungrounded >= 2)
                                {
                                    await FailAsync("Those controls are not on this screen, so I stopped.", ct);
                                    return;
                                }
                                continue;
                            }
                            if (stuckLabel is { } stuck && grounded[0].Target?.Label is { } label &&
                                GuideElementMatcher.SameLabel(label, stuck))
                            {
                                await FailAsync($"\"{label}\" did not change the screen, so I stopped.", ct);
                                return;
                            }
                            ungrounded = 0;
                            rejected = Array.Empty<string>();
                            stuckLabel = null;
                            stuckFingerprint = null;
                            ahead.AddRange(grounded);
                            break;
                    }
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

                SetPhase(new GuidePhase(GuidePhaseKind.Guiding, done.Count, done.Count + 1 + ahead.Count));
                switch (await PerformAsync(step, ct))
                {
                    case Outcome.Completed:
                        done.Add(step);
                        actions++;
                        misses = 0;
                        if (ChangesTheScreen(step) && step.Target?.Label is { } used)
                        {
                            var after = await _walker.SnapshotAsync();
                            if (GuideElementMatcher.Fingerprint(after) == before)
                            {
                                stuckLabel = used;
                                stuckFingerprint = before;
                                ahead.Clear();
                            }
                        }
                        break;

                    case Outcome.Skipped:
                        done.Add(step);
                        actions++;
                        misses = 0;
                        stuckLabel = null;
                        stuckFingerprint = null;
                        break;

                    case Outcome.NotFound:
                        misses++;
                        missed = step;
                        ahead.Clear();
                        stuckLabel = null;
                        stuckFingerprint = null;
                        if (misses >= MaxMisses)
                        {
                            await FailAsync($"I cannot find \"{step.Target?.Label ?? step.Title}\" on screen.", ct);
                            return;
                        }
                        break;

                    default:
                        return;
                }
            }

            if (!ct.IsCancellationRequested)
                await FailAsync("I stopped here before that was finished. Ask again to continue.", ct);
        }
        catch (OperationCanceledException)
        {
            // Stop() already cleaned up.
        }
        catch (Exception)
        {
            // Anything unexpected must still end the guide; otherwise the ghost stays on "Thinking...".
            if (!ct.IsCancellationRequested) await FailAsync("Something went wrong. Try asking again.", CancellationToken.None);
        }
    }

    // MARK: One step

    /// <summary>Clicks, toggles, and opens are supposed to change the screen. Typing and reading often do not.</summary>
    private static bool ChangesTheScreen(GuideStep step) =>
        step.Action is GuideAction.Click or GuideAction.Toggle or GuideAction.Open;

    private async Task<Outcome> PerformAsync(GuideStep step, CancellationToken ct)
    {
        var tag = TagFor(step);
        var events = Channel.CreateBounded<GuideStepEvent>(new BoundedChannelOptions(16)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        });
        _events = events;

        using var tickCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = TickAsync(events.Writer, tickCts.Token);
        TypingText = step.Action == GuideAction.Type ? step.Text : null;
        try
        {
            if (step.Target is not { } target)
            {
                _cursor.Follow(tag);
                return await WaitWithoutTargetAsync(step, events.Reader, ct);
            }

            var frame = await LocateAsync(target);
            var everFound = frame is not null;
            var misses = 0;
            if (frame is { } f) _cursor.PointAt(f.CenterX, f.CenterY, tag);
            else _cursor.Follow($"Looking for {target.Label}... (the shortcut skips)");

            await foreach (var e in events.Reader.ReadAllAsync(ct))
            {
                switch (e.Kind)
                {
                    case GuideStepEventKind.Skip:
                        return Outcome.Skipped;

                    case GuideStepEventKind.Click:
                        if (frame is { } current &&
                            (step.Action == GuideAction.Look || current.Contains(e.X, e.Y, margin: 6)))
                        {
                            await Task.Delay(400, ct);   // let the UI react before the next step
                            return Outcome.Completed;
                        }
                        break;

                    case GuideStepEventKind.ReturnKey:
                        if (step.Action is GuideAction.Type or GuideAction.Look) return Outcome.Completed;
                        break;

                    case GuideStepEventKind.Tick:
                        var found = await LocateAsync(target);
                        if (found is { } now)
                        {
                            misses = 0;
                            everFound = true;
                            if (frame is not { } old || now.MovedFrom(old)) _cursor.PointAt(now.CenterX, now.CenterY, tag);
                            frame = now;
                            break;
                        }

                        misses++;
                        // Typing and reading steps need no element to point at: keep the reminder by the pointer
                        // and wait for Return, rather than re-planning.
                        if (!everFound && misses >= 3 && step.Action is GuideAction.Type or GuideAction.Look)
                        {
                            _cursor.Follow(tag);
                            return await WaitWithoutTargetAsync(step, events.Reader, ct);
                        }
                        // Seen or not, a control that stays missing is a miss. Waiting forever is the loop;
                        // marking the step done because some other label is visible shows an invented next step.
                        if (misses >= MissingTicksBeforeReplan) return Outcome.NotFound;
                        if (everFound && misses >= 2)
                        {
                            frame = null;
                            _cursor.Follow($"Looking for {target.Label}...");
                        }
                        break;
                }
            }
            return Outcome.Cancelled;
        }
        finally
        {
            TypingText = null;
            tickCts.Cancel();
            events.Writer.TryComplete();
        }
    }

    /// <summary>Steps with no on-screen target, for example "wait for the list to load".</summary>
    private static async Task<Outcome> WaitWithoutTargetAsync(GuideStep step, ChannelReader<GuideStepEvent> reader, CancellationToken ct)
    {
        await foreach (var e in reader.ReadAllAsync(ct))
        {
            if (e.Kind is GuideStepEventKind.Skip or GuideStepEventKind.ReturnKey) return Outcome.Completed;
            if (e.Kind == GuideStepEventKind.Click && step.Action == GuideAction.Look) return Outcome.Completed;
        }
        return Outcome.Cancelled;
    }

    // MARK: Helpers

    /// <summary>The cursor tag: the action, then the one-sentence reason when the model gave one.</summary>
    private static string TagFor(GuideStep step)
    {
        var title = step.Action == GuideAction.Type && step.Text is { Length: > 0 } text
            ? $"Type \"{text}\", then press Return"
            : step.Title;
        var detail = step.Detail?.Trim();
        return string.IsNullOrEmpty(detail) || detail == title ? title : title + "\n" + detail;
    }

    private async Task<GuideRect?> LocateAsync(GuideTarget target)
    {
        var screen = await _walker.SnapshotAsync();
        return GuideElementMatcher.Best(target, screen, allowFieldFallback: false)?.Frame;
    }

    private static async Task TickAsync(ChannelWriter<GuideStepEvent> writer, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
                writer.TryWrite(new GuideStepEvent(GuideStepEventKind.Tick));
        }
        catch (OperationCanceledException) { }
    }

    private void StartInput()
    {
        _hook = new GuideInputHook();
        _hook.Click += (x, y) => _events?.Writer.TryWrite(new GuideStepEvent(GuideStepEventKind.Click, x, y));
        _hook.ReturnKey += () => _events?.Writer.TryWrite(new GuideStepEvent(GuideStepEventKind.ReturnKey));
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
        _events?.Writer.TryComplete();
        _events = null;
    }

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
