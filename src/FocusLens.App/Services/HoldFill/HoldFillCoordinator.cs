using System.Diagnostics;
using System.Windows.Threading;
using FocusLens.App.Views.HoldFill;
using FocusLens.Core.Guide;
using FocusLens.Core.HoldFill;
using FocusLens.Core.Logging;
using FocusLens.Platform.Windows.Guide;
using FocusLens.Platform.Windows.HoldFill;

namespace FocusLens.App.Services.HoldFill;

/// <summary>
/// Hold to fill: rest the pointer on an empty search field, a suggestion appears in a bubble
/// with a ring, and when the ring is full the text is typed in. Enter is never pressed.
///
/// Runs on the UI thread. Cheap at rest: a 10 Hz timer reads the pointer (one GetCursorPos), and
/// UI Automation is only asked once the pointer has stopped. While the bubble is up the timer
/// runs at 30 Hz so the ring fills smoothly.
/// </summary>
public sealed class HoldFillCoordinator : IDisposable
{
    private static readonly TimeSpan Resting = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan Active = TimeSpan.FromMilliseconds(33);

    private readonly GuideSettings _settings;
    private readonly IHoldFillSuggester _suggester;
    private readonly Func<Task<HoldFillContext>> _context;
    private readonly FileLog _log;
    private readonly UiaSearchFieldReader _reader = new();
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly HoldFillTracker _tracker;

    private HoldFillBubble? _bubble;
    private FoundSearchField? _found;
    private CancellationTokenSource? _suggesting;

    public HoldFillCoordinator(GuideSettings settings, IHoldFillSuggester suggester,
        Func<Task<HoldFillContext>> context, Dispatcher dispatcher, FileLog log)
    {
        _settings = settings;
        _suggester = suggester;
        _context = context;
        _log = log;
        _tracker = new HoldFillTracker(settings.HoldFillDuration);
        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = Resting };
        _timer.Tick += (_, _) => Tick();
    }

    public void Start()
    {
        _settings.Changed += Apply;
        Apply();
    }

    private void Apply()
    {
        _tracker.Hold = _settings.HoldFillDuration;
        if (_settings.HoldFill) { _timer.Start(); return; }
        _timer.Stop();
        CancelSuggestion();
        _tracker.Reset();
        _bubble?.Hide();
    }

    private void Tick()
    {
        var (x, y) = GuideOverlayStyle.CursorPixels();
        var frame = _tracker.Tick(x, y, _clock.Elapsed, HoldFillNative.AnyButtonDown());

        if (_found is not null && frame.Field?.Id != _found.Info.Id) CancelSuggestion();
        if (frame.LookUp) _ = LookUpAsync(x, y);
        if (frame.Fill && _found is { } found && frame.Suggestion is { } text) _ = FillAsync(found, text);

        Render(frame, x, y);
        _timer.Interval = frame.Stage == HoldFillStage.Idle ? Resting : Active;
    }

    private async Task LookUpAsync(int x, int y)
    {
        var found = await _reader.ReadAtAsync(x, y);
        if (!_tracker.OnLookup(found?.Info) || found is null) return;

        _found = found;
        var cts = _suggesting = new CancellationTokenSource();
        try
        {
            var context = await _context();
            var text = await _suggester.SuggestAsync(found.Info, context, cts.Token);
            _tracker.OnSuggestion(found.Info.Id, text);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.Warn($"Hold to fill: no suggestion ({ex.GetType().Name})");
            _tracker.OnSuggestion(found.Info.Id, null);
        }
    }

    private async Task FillAsync(FoundSearchField found, string text)
    {
        var typed = await UiaFieldText.TypeAsync(found.Element, text);
        // The suggestion itself is not logged: it is built from the user's activity.
        _log.Info(typed ? $"Hold to fill: filled a search field in {found.Info.AppName}"
                        : $"Hold to fill: could not type into {found.Info.AppName}");
    }

    private void Render(HoldFillFrame frame, int x, int y)
    {
        if (frame.Stage == HoldFillStage.Idle)
        {
            _bubble?.Hide();
            return;
        }
        _bubble ??= new HoldFillBubble();
        _bubble.Present(frame.Stage, frame.Progress, frame.Suggestion);
        _bubble.PlaceNear(x, y);
    }

    private void CancelSuggestion()
    {
        _suggesting?.Cancel();
        _suggesting = null;
        _found = null;
    }

    public void Dispose()
    {
        _settings.Changed -= Apply;
        _timer.Stop();
        CancelSuggestion();
        _bubble?.Close();
        _bubble = null;
    }
}
