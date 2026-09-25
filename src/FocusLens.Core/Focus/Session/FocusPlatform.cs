namespace FocusLens.Core.Focus.Session;

/// <summary>Reads the foreground app, window title and, for browsers, the URL.</summary>
public interface IFocusContextReader
{
    /// <summary>
    /// Null for FocusLens itself and for system surfaces (desktop, taskbar, Start), which are never a distraction.
    /// </summary>
    /// <param name="allowStale">When the app is too busy to answer, return its last reading from the last 10 s.
    /// Closing passes false, so it never acts on a remembered page.</param>
    Task<FocusContext?> CurrentAsync(bool allowStale = true);
}

public enum FocusCloseOutcome
{
    Closed,
    Failed,
}

/// <summary>Closes the current tab (browsers) or the window (other apps). Never quits an app.</summary>
public interface IFocusWindowCloser
{
    Task<FocusCloseOutcome> CloseAsync(FocusContext target);
}

/// <summary>Tells the controller when to look, so it can look less often.</summary>
public interface IFocusActivityWatcher
{
    /// <summary>Screen locked, session switched away, or the PC suspended: no reads, and the bar does not drain.</summary>
    bool IsScreenAsleep { get; }

    /// <param name="onAppSwitch">Called on the thread that called Start when another window comes to the front.</param>
    void Start(Action onAppSwitch);
    void Stop();

    /// <summary>Seconds since the last keyboard or mouse input.</summary>
    double IdleSeconds();
}

/// <summary>Timers on the UI thread. The controller is single-threaded and relies on this.</summary>
public interface IFocusScheduler
{
    IDisposable Every(TimeSpan interval, Action action);
    void After(TimeSpan delay, Action action);
}
