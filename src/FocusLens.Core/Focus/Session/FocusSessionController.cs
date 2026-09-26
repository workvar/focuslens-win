using FocusLens.Core.Focus.Classification;

namespace FocusLens.Core.Focus.Session;

/// <summary>
/// Owns one focus session: the goal, the clock, and what the surfaces show. The other parts:
///
///   .Monitor   looks at the foreground window and asks the classifier
///   .Enforce   drives the patience bar and the close or block action
///   .Record    saves the finished session
///
/// Single-threaded: every member runs on the UI thread (the scheduler's timers fire there and
/// awaits resume there). The foreground window is read off that thread by the reader. When:
///   - right after an app switch (<see cref="IFocusActivityWatcher"/>)
///   - every <see cref="FocusSettings.PollInterval"/> seconds for changes inside an app
///   - every second while a countdown or block overlay is up
///   - never while the screen is locked or the PC is asleep, or the user is away
/// </summary>
public sealed partial class FocusSessionController
{
    /// <summary>No input for this long counts as away.</summary>
    public const double IdleAfterSeconds = 60;

    private readonly IFocusContextReader _reader;
    private readonly IFocusWindowCloser _closer;
    private readonly IFocusActivityWatcher _activity;
    private readonly IFocusScheduler _scheduler;

    private EscalationPolicy _policy = new(FocusSettings.DefaultPatience);
    private FocusScoreTracker _score = new();
    private FocusEpisodeTracker _episodes = new();
    private FocusObservation _observation = FocusObservation.Neutral;
    private FocusContext? _observedContext;
    /// <summary>The most recent reading of the foreground window.</summary>
    private FocusContext? _latestContext;
    /// <summary>A page only the model can judge, and when it was first seen.</summary>
    private (FocusContext Context, DateTime Since)? _pendingJudgement;
    private DateTime _nextReadAt = DateTime.MinValue;
    private bool _isClassifying;
    private bool _isReadingContext;
    private FocusContext? _countdownTarget;
    /// <summary>The window Focus Mode just closed, ignored until the time given (see ClosingGrace).</summary>
    private (FocusContext Target, DateTime Until)? _recentlyClosed;
    private Guid _flashToken;
    private IDisposable? _ticker;
    private DateTime _lastTickAt;

    public FocusSessionController(
        IFocusClassifier classifier, FocusSettings settings, FocusSessionStore store,
        IFocusContextReader reader, IFocusWindowCloser closer, IFocusActivityWatcher activity, IFocusScheduler scheduler)
    {
        Classifier = classifier;
        Settings = settings;
        Store = store;
        _reader = reader;
        _closer = closer;
        _activity = activity;
        _scheduler = scheduler;
    }

    public IFocusClassifier Classifier { get; }
    public FocusSettings Settings { get; }
    public FocusSessionStore Store { get; }

    public FocusSession? Session { get; private set; }
    /// <summary>The countdown, "closed" and "complete" messages.</summary>
    public FocusAlert? Alert { get; private set; }
    /// <summary>The patience bar while the user is off topic.</summary>
    public Distraction? Distraction { get; private set; }
    /// <summary>The window covered by the block overlay.</summary>
    public FocusContext? BlockTarget { get; private set; }
    public FocusSessionRecord? LastRecord { get; private set; }
    public int LiveScore { get; private set; } = 100;
    /// <summary>The patience bar, 1 is full.</summary>
    public double MeterLevel { get; private set; } = 1;
    /// <summary>"42m" for the tray while a session runs, null otherwise. Changes once a minute.</summary>
    public string? TrayText { get; private set; }

    public bool IsActive => Session is not null;

    /// <summary>Session, alert, distraction, block target or last record changed.</summary>
    public event Action? Changed;
    /// <summary>Score or patience bar changed. Up to once a second.</summary>
    public event Action? LiveChanged;
    public event Action? TrayTextChanged;
    /// <summary>The bar reached halfway. The host plays a sound.</summary>
    public event Action? Nudged;

    // Lifecycle

    public void Start(string goal, int minutes, FocusEnforcement enforcement)
    {
        var trimmed = goal.Trim();
        if (trimmed.Length == 0 || Session is not null) return;

        var patience = Settings.Patience;
        _policy = new EscalationPolicy(patience);
        _score = new FocusScoreTracker();
        _episodes = new FocusEpisodeTracker();
        _observation = FocusObservation.Neutral;
        _observedContext = null;
        _latestContext = null;
        _pendingJudgement = null;
        _nextReadAt = DateTime.MinValue;
        _countdownTarget = null;
        _isReadingContext = false;
        _lastTickAt = DateTime.UtcNow;
        LastRecord = null;
        Alert = null;
        Distraction = null;
        BlockTarget = null;
        SetLive(100, 1);

        Session = new FocusSession(Guid.NewGuid(), trimmed, DateTime.UtcNow,
            TimeSpan.FromMinutes(minutes), enforcement, TimeSpan.FromSeconds(patience));
        UpdateTrayText();
        // Give the new app a moment to put its window in front.
        _activity.Start(() => RequestRead(TimeSpan.FromSeconds(0.5)));
        _ticker = _scheduler.Every(TimeSpan.FromSeconds(1), Tick);
        Changed?.Invoke();
    }

    public void Stop() => Finish(completed: false);

    // Alerts

    /// <summary>Shows a message and clears it after <paramref name="duration"/>, unless something newer replaced it.</summary>
    private void Flash(FocusAlert message, TimeSpan duration)
    {
        SetAlert(message);
        var token = _flashToken;
        _scheduler.After(duration, () =>
        {
            if (_flashToken != token) return;
            Alert = null;
            Changed?.Invoke();
        });
    }

    private void SetAlert(FocusAlert? message)
    {
        _flashToken = Guid.NewGuid();
        if (Alert == message) return;
        Alert = message;
        Changed?.Invoke();
    }

    // Tick

    private void Tick()
    {
        if (Session is not { } session) return;
        var now = DateTime.UtcNow;
        if (session.Remaining(now) <= TimeSpan.Zero)
        {
            Finish(completed: true);
            return;
        }

        var dt = Math.Clamp((now - _lastTickAt).TotalSeconds, 0, 5);
        _lastTickAt = now;

        UpdateTrayText();
        // Locked or asleep: nobody is looking at anything, so nothing drains.
        if (_activity.IsScreenAsleep && _observation.Kind != FocusObservationKind.Neutral) _observation = FocusObservation.Neutral;
        AdvanceMeter(dt, now);
        ReadContextIfNeeded(session.Id, now);
    }

    /// <summary>Brings the next reading forward. The tick runs once a second, so it happens on the first tick after the delay.</summary>
    private void RequestRead(TimeSpan delay)
    {
        var at = DateTime.UtcNow + delay;
        if (at < _nextReadAt) _nextReadAt = at;
    }

    /// <summary>Skipped while the previous read is still waiting on a slow app.</summary>
    private async void ReadContextIfNeeded(Guid sessionId, DateTime now)
    {
        if (_isReadingContext || _activity.IsScreenAsleep) return;
        var enforcing = _countdownTarget is not null || BlockTarget is not null;
        if (!enforcing && now < _nextReadAt) return;
        // Away from the keyboard: the screen is not changing. The idle query runs only when a reading is due.
        if (!enforcing && Settings.PauseWhenIdle && _activity.IdleSeconds() >= IdleAfterSeconds) return;

        _nextReadAt = now.AddSeconds(Settings.PollInterval);
        _isReadingContext = true;
        FocusContext? context;
        try { context = await _reader.CurrentAsync(); }
        catch { context = null; }
        _isReadingContext = false;
        if (Session?.Id != sessionId) return;

        _latestContext = context;
        AdvanceCountdown(context);
        VerifyBlockTarget(context);
        MonitorTick(context);
    }

    private void UpdateTrayText()
    {
        var text = Session is { } session ? FocusFormat.ShortRemaining(session.Remaining(DateTime.UtcNow)) : null;
        if (text == TrayText) return;
        TrayText = text;
        TrayTextChanged?.Invoke();
    }

    private void SetLive(int score, double meter)
    {
        if (score == LiveScore && meter.Equals(MeterLevel)) return;
        LiveScore = score;
        MeterLevel = meter;
        LiveChanged?.Invoke();
    }
}
