namespace FocusLens.Core.HoldFill;

public enum HoldFillStage { Idle, Suggesting, Holding, Filled }

/// <summary>What to show after a tick, and what the coordinator should start.</summary>
/// <param name="Progress">0 to 1, how full the ring is.</param>
/// <param name="LookUp">Read the field under the pointer, then call <see cref="HoldFillTracker.OnLookup"/>.</param>
/// <param name="Fill">Type <paramref name="Suggestion"/> into <paramref name="Field"/> now.</param>
public readonly record struct HoldFillFrame(
    HoldFillStage Stage,
    double Progress,
    string? Suggestion,
    SearchField? Field,
    bool LookUp,
    bool Fill);

/// <summary>
/// The hold-to-fill state machine. Pure: it is fed the pointer position and the time, and says
/// what to do, so it can be tested without a screen.
///
///   pointer rests 0.35 s          look up what is under it (once per resting place)
///   an empty search field         ask for a suggestion (the coordinator does, on LookUp's answer)
///   suggestion ready              the ring fills while the pointer stays within a few pixels
///   ring full                     Fill, once; nothing more until the pointer leaves the field
///   pointer moves                 the ring starts again from empty
///   pointer leaves the field      everything resets
/// </summary>
public sealed class HoldFillTracker
{
    public static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(350);

    /// <summary>How long "Filled" stays on screen.</summary>
    public static readonly TimeSpan FilledShown = TimeSpan.FromSeconds(1.2);

    private readonly double _tolerance;

    private (double X, double Y) _anchor = (double.NaN, double.NaN);
    private TimeSpan _anchorAt;
    private bool _lookedUpHere;
    private bool _lookupPending;

    private SearchField? _field;
    private bool _suggestPending;
    private string? _suggestion;
    private TimeSpan? _holdStart;
    private TimeSpan? _filledAt;

    /// <summary>Filled, or nothing to offer here. Nothing happens until the pointer leaves the field.</summary>
    private bool _done;

    /// <param name="tolerancePixels">How far the pointer may drift and still count as held.</param>
    public HoldFillTracker(TimeSpan hold, double tolerancePixels = 10)
    {
        Hold = hold;
        _tolerance = tolerancePixels;
    }

    public TimeSpan Hold { get; set; }

    /// <summary>The field being offered or waited on, if any.</summary>
    public SearchField? Field => _field;

    /// <param name="buttonDown">A mouse button is held (dragging, selecting): never counts as resting.</param>
    public HoldFillFrame Tick(double x, double y, TimeSpan now, bool buttonDown = false)
    {
        var lookUp = false;
        var fill = false;

        if (buttonDown || Distance(x, y) > _tolerance)
        {
            _anchor = (x, y);
            _anchorAt = now;
            _lookedUpHere = false;
            _holdStart = null;
        }

        if (_field is { } field && !field.Frame.Contains(x, y)) Reset();

        if (_field is null)
        {
            if (!buttonDown && !_lookupPending && !_lookedUpHere && now - _anchorAt >= Settle)
            {
                _lookedUpHere = true;
                _lookupPending = true;
                lookUp = true;
            }
        }
        else if (!_done && _suggestion is not null)
        {
            _holdStart ??= now;
            if (now - _holdStart.Value >= Hold)
            {
                fill = true;
                _done = true;
                _filledAt = now;
            }
        }

        return new HoldFillFrame(StageAt(now), ProgressAt(now), _suggestion, _field, lookUp, fill);
    }

    /// <summary>
    /// The answer to <see cref="HoldFillFrame.LookUp"/>. Returns true when a suggestion should be
    /// fetched for <paramref name="field"/>.
    /// </summary>
    public bool OnLookup(SearchField? field)
    {
        _lookupPending = false;
        if (field is null || _field is not null) return false;
        // The pointer may have moved on while the field was being read.
        if (!field.Frame.Contains(_anchor.X, _anchor.Y)) return false;

        _field = field;
        _done = !field.IsEmpty;
        _suggestPending = !_done;
        return _suggestPending;
    }

    /// <summary>The suggestion for a field, or null when there is nothing sensible to offer.</summary>
    public void OnSuggestion(string fieldId, string? text)
    {
        if (_field?.Id != fieldId || !_suggestPending) return;
        _suggestPending = false;
        if (string.IsNullOrWhiteSpace(text)) { _done = true; return; }
        _suggestion = text.Trim();
        _holdStart = null;
    }

    /// <summary>Forget everything, for example when the feature is turned off.</summary>
    public void Reset()
    {
        _field = null;
        _suggestPending = false;
        _suggestion = null;
        _holdStart = null;
        _filledAt = null;
        _done = false;
    }

    private HoldFillStage StageAt(TimeSpan now)
    {
        if (_filledAt is { } at) return now - at < FilledShown ? HoldFillStage.Filled : HoldFillStage.Idle;
        if (_field is null || _done) return HoldFillStage.Idle;
        if (_suggestion is not null) return HoldFillStage.Holding;
        return _suggestPending ? HoldFillStage.Suggesting : HoldFillStage.Idle;
    }

    private double ProgressAt(TimeSpan now)
    {
        if (_filledAt is not null) return 1;
        if (_holdStart is not { } start || Hold <= TimeSpan.Zero) return 0;
        return Math.Clamp((now - start) / Hold, 0, 1);
    }

    private double Distance(double x, double y)
    {
        if (double.IsNaN(_anchor.X)) return double.PositiveInfinity;
        var dx = x - _anchor.X;
        var dy = y - _anchor.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
