namespace FocusLens.Core.Health;

/// <summary>Checks one dependency of the app and reports whether it is usable right now.</summary>
public interface IHealthProbe
{
    string Name { get; }

    /// <summary>Slow or networked probes ask the monitor not to re-run them more often than this.</summary>
    TimeSpan MinInterval { get; }

    Task<ServiceHealth> CheckAsync(CancellationToken ct);
}
