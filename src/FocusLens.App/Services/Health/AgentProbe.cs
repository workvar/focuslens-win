using FocusLens.Core.Health;
using FocusLens.Core.Settings;

namespace FocusLens.App.Services;

/// <summary>The agent is "connected" when its process is alive and its heartbeat is fresh.</summary>
public sealed class AgentProbe : IHealthProbe
{
    /// <summary>The agent writes a heartbeat every 10 seconds; three misses means it is stuck.</summary>
    private const double StaleAfterSeconds = 30;

    private readonly AgentLauncher _agent;

    public AgentProbe(AgentLauncher agent) => _agent = agent;

    public string Name => "Tracking agent";
    public TimeSpan MinInterval => TimeSpan.FromSeconds(4);

    public Task<ServiceHealth> CheckAsync(CancellationToken ct) => Task.FromResult(Check());

    private ServiceHealth Check()
    {
        if (!_agent.IsRunning())
        {
            return _agent.AgentExists
                ? ServiceHealth.Disconnected(Name, "Not running. Start it in Settings > General")
                : ServiceHealth.Disconnected(Name, "FocusLensAgent.exe was not found next to the app");
        }

        var shared = SharedState.Read();
        var age = SharedState.NowUnix() - shared.AgentHeartbeat;

        if (shared.AgentHeartbeat <= 0 || age > StaleAfterSeconds)
            return ServiceHealth.Degraded(Name, "Running, but it has not reported in recently");
        if (shared.TrackingPaused)
            return ServiceHealth.Degraded(Name, "Running, tracking is paused");
        return ServiceHealth.Connected(Name, $"Tracking, last heartbeat {Math.Max(0, (int)age)}s ago");
    }
}
