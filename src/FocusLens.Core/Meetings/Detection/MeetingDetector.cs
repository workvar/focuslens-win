namespace FocusLens.Core.Meetings.Detection;

/// <summary>
/// Watches which processes hold the microphone and proposes a meeting when a known
/// conferencing app (or a browser tab on a known meeting URL) keeps capturing audio.
/// Call <see cref="Tick"/> every couple of seconds.
/// </summary>
public sealed class MeetingDetector
{
    private static readonly TimeSpan MinimumStream = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan LowConfidenceMinimum = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan EndGrace = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan DismissalCooldown = TimeSpan.FromSeconds(60);

    private readonly IAudioProcessMonitor _monitor;
    private readonly IProcessInspector _processes;
    private readonly IBrowserInspector _browsers;
    private readonly MeetingProviderRegistry _registry;
    private readonly Func<DetectionOptions> _options;
    private readonly Func<DateTime> _now;

    private readonly Dictionary<int, DateTime> _firstSeen = new();
    private readonly Dictionary<int, DateTime> _releasedAt = new();
    private readonly Dictionary<string, DateTime> _dismissedUntil = new();
    private int? _announcedPid;
    private int? _activePid;

    public event Action<MeetingCandidate, MatchConfidence>? CandidateFound;
    public event Action? SessionEnded;

    public MeetingDetector(
        IAudioProcessMonitor monitor, IProcessInspector processes, IBrowserInspector browsers,
        MeetingProviderRegistry registry, Func<DetectionOptions> options, Func<DateTime>? now = null)
    {
        _monitor = monitor;
        _processes = processes;
        _browsers = browsers;
        _registry = registry;
        _options = options;
        _now = now ?? (() => DateTime.UtcNow);
    }

    public bool IsWatchingSession => _activePid is not null;

    public void NoteDismissal(MeetingProvider provider)
    {
        _dismissedUntil[provider.Id()] = _now() + DismissalCooldown;
        _announcedPid = null;
    }

    public void NoteSessionStarted() => _activePid = _announcedPid;

    public void NoteSessionEnded()
    {
        _activePid = null;
        _announcedPid = null;
    }

    public bool IsCoolingDown(MeetingProvider provider) =>
        _dismissedUntil.TryGetValue(provider.Id(), out var until) && _now() < until;

    public void Tick()
    {
        if (!_options().Enabled) return;

        var capturing = Attributed(_monitor.CapturingProcesses());
        var currentPids = capturing.Select(c => c.Pid).ToHashSet();

        TrackLifetimes(capturing, currentPids);
        CheckForSessionEnd(currentPids);
        if (_activePid is not null) return;

        foreach (var state in capturing)
        {
            if (Evaluate(state) is { } found)
            {
                _announcedPid = state.Pid;
                CandidateFound?.Invoke(found.Candidate, found.Confidence);
                return;
            }
        }
    }

    private List<AudioProcessState> Attributed(IEnumerable<AudioProcessState> states)
    {
        var seen = new HashSet<int>();
        var result = new List<AudioProcessState>();
        foreach (var state in states)
        {
            var owner = _processes.OwningPid(state.Pid);
            if (seen.Add(owner))
                result.Add(state with { Pid = owner });
        }
        return result;
    }

    private void TrackLifetimes(List<AudioProcessState> capturing, HashSet<int> current)
    {
        var now = _now();
        foreach (var state in capturing) _firstSeen.TryAdd(state.Pid, now);
        foreach (var pid in _firstSeen.Keys.Where(p => !current.Contains(p)))
            _releasedAt.TryAdd(pid, now);
        foreach (var pid in current) _releasedAt.Remove(pid);

        foreach (var (pid, released) in _releasedAt.ToList())
        {
            if (pid != _activePid && now - released >= EndGrace)
            {
                _firstSeen.Remove(pid);
                _releasedAt.Remove(pid);
            }
        }
    }

    private void CheckForSessionEnd(HashSet<int> current)
    {
        if (_activePid is not { } pid) return;

        if (!_processes.IsRunning(pid))
        {
            CleanUp(pid);
            SessionEnded?.Invoke();
            return;
        }
        if (current.Contains(pid) || !_releasedAt.TryGetValue(pid, out var released)) return;
        if (_now() - released >= EndGrace)
        {
            CleanUp(pid);
            SessionEnded?.Invoke();
        }
    }

    private void CleanUp(int pid)
    {
        _activePid = null;
        _announcedPid = null;
        _firstSeen.Remove(pid);
        _releasedAt.Remove(pid);
    }

    private (MeetingCandidate Candidate, MatchConfidence Confidence)? Evaluate(AudioProcessState state)
    {
        if (_announcedPid == state.Pid) return null;
        var appId = _processes.AppIdOf(state.Pid);
        if (appId is null) return null;

        var options = _options();
        if (options.DenyList.Contains(appId)) return null;
        if (!_firstSeen.TryGetValue(state.Pid, out var started) || _now() - started < MinimumStream) return null;

        var match = ResolveProvider(appId, state.Pid);
        if (match is null) return null;

        var isChatApp = match.Provider is MeetingProvider.Slack or MeetingProvider.Discord;
        if (isChatApp && !options.IncludeChatApps) return null;
        if (match.Confidence == MatchConfidence.Low && _now() - started < LowConfidenceMinimum) return null;
        if (IsCoolingDown(match.Provider)) return null;

        return (new MeetingCandidate
        {
            Provider = match.Provider,
            Source = MeetingSource.Detected,
            AppId = appId,
            ConferencingUrl = match.ConferencingUrl,
            DetectedAt = _now(),
        }, match.Confidence);
    }

    private MeetingProviderMatch? ResolveProvider(string appId, int pid)
    {
        if (_registry.Match(appId) is { } direct) return direct;
        if (!_browsers.IsBrowser(appId)) return null;

        var urls = _browsers.TabUrls(appId);
        if (urls.Count > 0 && _registry.MatchUrls(urls) is { } byUrl) return byUrl;
        return _registry.MatchTitles(_browsers.WindowTitles(pid));
    }
}
