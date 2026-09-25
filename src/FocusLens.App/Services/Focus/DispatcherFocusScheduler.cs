using System.Windows.Threading;
using FocusLens.Core.Focus.Session;

namespace FocusLens.App.Services.Focus;

/// <summary>Focus Mode timers on the WPF dispatcher, so the controller only ever runs on the UI thread.</summary>
public sealed class DispatcherFocusScheduler : IFocusScheduler
{
    public IDisposable Every(TimeSpan interval, Action action)
    {
        // Background priority lets input and rendering go first; a tick a few ms late does not matter.
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = interval };
        timer.Tick += (_, _) => action();
        timer.Start();
        return new Stopper(timer);
    }

    public void After(TimeSpan delay, Action action)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = delay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };
        timer.Start();
    }

    private sealed class Stopper(DispatcherTimer timer) : IDisposable
    {
        public void Dispose() => timer.Stop();
    }
}
