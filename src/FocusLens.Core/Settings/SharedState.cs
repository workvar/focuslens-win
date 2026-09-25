using FocusLens.Core.Paths;
using FocusLens.Core.Storage;

namespace FocusLens.Core.Settings;

/// <summary>
/// Small cross-process state (replaces the macOS shared UserDefaults suite):
/// pause flag written by the app, heartbeat and recording status written by the agent.
/// </summary>
public sealed class SharedStateData
{
    public bool TrackingPaused { get; set; }
    public double AgentHeartbeat { get; set; }
    public bool RecordingRestricted { get; set; }
    public string? RecordingRestrictedReason { get; set; }
    public string? RecordingRestrictedApp { get; set; }
    public double RecordingStatusAt { get; set; }
}

public static class SharedState
{
    private static readonly object Gate = new();

    public static SharedStateData Read() =>
        JsonFile.Load(AppPaths.SharedStateFile, () => new SharedStateData());

    /// <summary>Read-modify-write. Last writer wins; fields are owned by one process each.</summary>
    public static void Update(Action<SharedStateData> change)
    {
        lock (Gate)
        {
            var data = Read();
            change(data);
            JsonFile.Save(AppPaths.SharedStateFile, data);
        }
    }

    public static bool IsTrackingPaused() => Read().TrackingPaused;

    public static double NowUnix() =>
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
}
