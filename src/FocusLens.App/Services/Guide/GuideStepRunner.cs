using System.Threading.Channels;
using FocusLens.Core.Guide;
using FocusLens.Platform.Windows.Guide;

namespace FocusLens.App.Services.Guide;

/// <summary>
/// One step, from the moment the ghost points at it to the moment the user has done it. Split out
/// of the session controller so that file can be read as the shape of a whole task.
///
/// A step is resolved against the live screen when it starts and re-checked once a second while it
/// waits, so the ghost follows a window the user drags and notices when the target has gone. A step
/// ends when the user clicks its target, presses Return (typing steps), or asks to skip.
/// </summary>
public sealed class GuideStepRunner
{
    public enum Outcome { Completed, Skipped, NotFound, Cancelled }

    private readonly UiaWalker _walker;
    private readonly GuideCursorController _cursor;
    private Channel<GuideStepEvent>? _events;

    /// <summary>The text the current step asks the user to type, if it is a typing step. Hold to fill offers it.</summary>
    public string? TypingText { get; private set; }

    public GuideStepRunner(UiaWalker walker, GuideCursorController cursor)
    {
        _walker = walker;
        _cursor = cursor;
    }

    /// <summary>Clicks and key presses seen by the session's input hook.</summary>
    public void Post(GuideStepEvent e) => _events?.Writer.TryWrite(e);

    public void Skip() => Post(new GuideStepEvent(GuideStepEventKind.Skip));

    public void End()
    {
        _events?.Writer.TryComplete();
        _events = null;
        TypingText = null;
    }

    public async Task<Outcome> RunAsync(GuideStep step, CancellationToken ct)
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
                        if (misses >= GuideRecovery.MissingTicksBeforeReplan) return Outcome.NotFound;
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
    private static async Task<Outcome> WaitWithoutTargetAsync(GuideStep step, ChannelReader<GuideStepEvent> reader,
        CancellationToken ct)
    {
        await foreach (var e in reader.ReadAllAsync(ct))
        {
            if (e.Kind is GuideStepEventKind.Skip or GuideStepEventKind.ReturnKey) return Outcome.Completed;
            if (e.Kind == GuideStepEventKind.Click && step.Action == GuideAction.Look) return Outcome.Completed;
        }
        return Outcome.Cancelled;
    }

    /// <summary>The cursor tag: the action, then the one-sentence reason when the model gave one.</summary>
    public static string TagFor(GuideStep step)
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
}
