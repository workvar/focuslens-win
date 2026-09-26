using System.Diagnostics;
using System.Windows.Threading;
using FocusLens.App.Views.Guide;
using FocusLens.Core.Guide;
using FocusLens.Platform.Windows.Guide;

namespace FocusLens.App.Services.Guide;

/// <summary>
/// Owns the ghost cursor: its window, its follower and its frame timer.
///
/// Cheap when idle. Windows has no permission-free "mouse moved" event, so instead of a hook the
/// timer runs slowly (10 Hz, one GetCursorPos) while the ghost is settled and speeds up to
/// about 60 Hz only while it is travelling. This matters because FocusLens shares the machine
/// with whatever the user works in.
/// </summary>
public sealed class GuideCursorController
{
    private const double FollowOffsetX = 18, FollowOffsetY = 22;
    private const double GlideRate = 6;
    private static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(16);
    private static readonly TimeSpan Slow = TimeSpan.FromMilliseconds(100);

    private readonly GuideSettings _settings;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();

    private GuideCursorWindow? _window;
    private GuideFollower _follower = new(0, 0);
    private (double X, double Y)? _pointTarget;
    private TimeSpan _last;

    public GuideCursorController(GuideSettings settings, Dispatcher dispatcher)
    {
        _settings = settings;
        _timer = new DispatcherTimer(DispatcherPriority.Render, dispatcher) { Interval = Slow };
        _timer.Tick += (_, _) => Tick();
    }

    public void Show()
    {
        if (_window is not null) return;
        _window = new GuideCursorWindow();
        _window.Show();
        var (x, y) = FollowTarget();
        _follower = new GuideFollower(x, y, _settings.FollowRate);
        _window.PlaceTip(x, y);
        _clock.Restart();
        _last = _clock.Elapsed;
        _timer.Start();
    }

    public void Hide()
    {
        _timer.Stop();
        _window?.Close();
        _window = null;
        _pointTarget = null;
    }

    /// <summary>Trail the user's pointer. Used while thinking, between steps and at rest.</summary>
    public void Follow(string tag)
    {
        _pointTarget = null;
        _follower.Rate = _settings.FollowRate;
        _window?.SetPointing(false);
        _window?.SetTag(tag, _settings.ShowTag);
        Wake();
    }

    /// <summary>Glide to a screen pixel and rest there.</summary>
    public void PointAt(double x, double y, string tag)
    {
        _pointTarget = (x, y);
        _follower.Rate = GlideRate;
        _window?.SetPointing(true);
        _window?.SetTag(tag, _settings.ShowTag);
        Wake();
    }

    public void Celebrate(string tag)
    {
        _pointTarget = null;
        _window?.SetPointing(false);
        _window?.SetTag(tag, _settings.ShowTag);
        Wake();
    }

    private (double X, double Y) FollowTarget()
    {
        var (mx, my) = GuideOverlayStyle.CursorPixels();
        var scale = _window?.Scale ?? 1;
        return (mx + FollowOffsetX * scale, my + FollowOffsetY * scale);
    }

    private void Wake()
    {
        if (_window is null) return;
        _last = _clock.Elapsed;
        _timer.Interval = Fast;
    }

    private void Tick()
    {
        if (_window is null) return;
        var now = _clock.Elapsed;
        var dt = Math.Min((now - _last).TotalSeconds, 0.033);   // a wake from slow polling must not jump
        _last = now;

        var (tx, ty) = _pointTarget ?? FollowTarget();
        if (_follower.HasSettled(tx, ty))
        {
            // At rest: poll slowly. A pointer move makes HasSettled false on a later tick.
            _timer.Interval = Slow;
            return;
        }
        _timer.Interval = Fast;
        _follower.Advance(tx, ty, dt);
        _window.PlaceTip(_follower.X, _follower.Y);
    }
}
