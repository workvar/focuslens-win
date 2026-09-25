namespace FocusLens.App.Services;

public enum UpdateStatus
{
    Idle,
    Checking,
    UpToDate,
    Available,
    Downloading,
    Ready,
    Failed,
}

/// <summary>Snapshot of the updater. <see cref="Error"/> on an Available state means a download attempt failed.</summary>
public sealed record UpdateState(
    UpdateStatus Status,
    string? Version = null,
    string? Notes = null,
    int Progress = 0,
    string? Error = null);
