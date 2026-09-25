namespace FocusLens.Core.Health;

public enum ServiceState
{
    Checking,
    Connected,
    /// <summary>Reachable but not fully usable, for example a model that is not pulled yet.</summary>
    Degraded,
    Disconnected,
}

public sealed record ServiceHealth(string Name, ServiceState State, string Detail)
{
    public static ServiceHealth Connected(string name, string detail) => new(name, ServiceState.Connected, detail);
    public static ServiceHealth Degraded(string name, string detail) => new(name, ServiceState.Degraded, detail);
    public static ServiceHealth Disconnected(string name, string detail) => new(name, ServiceState.Disconnected, detail);
}
