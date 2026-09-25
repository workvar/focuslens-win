namespace FocusLens.Core.Meetings.Detection;

/// <summary>A process that currently has an audio session open.</summary>
public sealed record AudioProcessState(int Pid, bool IsRunningInput, bool IsRunningOutput);

/// <summary>Lists processes that are actively capturing from a microphone.</summary>
public interface IAudioProcessMonitor
{
    IReadOnlyList<AudioProcessState> CapturingProcesses();
}

/// <summary>Process lookups the detector needs, kept behind an interface so it stays testable.</summary>
public interface IProcessInspector
{
    /// <summary>Executable name (lowercase) of the process, or null if it is gone.</summary>
    string? AppIdOf(int pid);
    /// <summary>The top-level owner of a helper process (a browser's audio service maps to the browser).</summary>
    int OwningPid(int pid);
    bool IsRunning(int pid);
}

public interface IBrowserInspector
{
    bool IsBrowser(string appId);
    IReadOnlyList<string> TabUrls(string appId);
    IReadOnlyList<string> WindowTitles(int pid);
}

/// <summary>User-controlled detection behaviour, re-read on every tick.</summary>
public sealed record DetectionOptions(bool Enabled, bool IncludeChatApps, IReadOnlySet<string> DenyList);
