namespace FocusLens.Core.Health;

/// <summary>
/// Runs a fixed set of probes and remembers each result. Callers may poll <see cref="RefreshAsync"/>
/// as often as they like; every probe is throttled by its own MinInterval unless forced.
/// </summary>
public sealed class HealthMonitor
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(30);

    private readonly IReadOnlyList<IHealthProbe> _probes;
    private readonly Dictionary<string, DateTime> _lastRun = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public HealthMonitor(IEnumerable<IHealthProbe> probes) => _probes = probes.ToList();

    public IReadOnlyList<string> Names => _probes.Select(p => p.Name).ToList();

    /// <summary>Raised from a background thread each time a probe finishes.</summary>
    public event Action<ServiceHealth>? Updated;

    public async Task RefreshAsync(bool force = false, CancellationToken ct = default)
    {
        if (force) await _gate.WaitAsync(ct);
        else if (!await _gate.WaitAsync(0, ct)) return; // a refresh is already in flight

        try
        {
            var due = _probes.Where(p => force || IsDue(p)).ToList();
            await Task.WhenAll(due.Select(p => RunAsync(p, ct)));
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool IsDue(IHealthProbe probe)
    {
        lock (_lastRun)
            return !_lastRun.TryGetValue(probe.Name, out var at) || DateTime.UtcNow - at >= probe.MinInterval;
    }

    private async Task RunAsync(IHealthProbe probe, CancellationToken ct)
    {
        ServiceHealth result;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(ProbeTimeout);
            result = await probe.CheckAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            result = ServiceHealth.Disconnected(probe.Name, "The check timed out");
        }
        catch (Exception ex)
        {
            result = ServiceHealth.Disconnected(probe.Name, ex.Message);
        }

        lock (_lastRun) _lastRun[probe.Name] = DateTime.UtcNow;
        Updated?.Invoke(result);
    }
}
