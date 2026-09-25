using System.Diagnostics;
using FocusLens.Core.Settings;
using FocusLens.Platform.Windows.Shell;

namespace FocusLens.App.Services;

public enum AgentStatus
{
    Running,
    Stopped,
    Paused,
}

/// <summary>Starts, stops and monitors the background tracking agent, and manages start-at-login.</summary>
public sealed class AgentLauncher
{
    private const string AutoStartName = "FocusLensAgent";
    private const string StopEventName = @"Local\FocusLensAgent.Stop";
    private const string MutexName = @"Local\FocusLensAgent.Single";

    private static string AgentPath =>
        Path.Combine(AppContext.BaseDirectory, "FocusLensAgent.exe");

    public bool AgentExists => File.Exists(AgentPath);

    /// <summary>The agent holds a named mutex while it runs, which is the most reliable liveness check.</summary>
    public bool IsRunning()
    {
        try
        {
            using var mutex = Mutex.OpenExisting(MutexName);
            return true;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true; // exists but owned by another session
        }
    }

    public AgentStatus Status()
    {
        if (!IsRunning()) return AgentStatus.Stopped;
        return SharedState.IsTrackingPaused() ? AgentStatus.Paused : AgentStatus.Running;
    }

    public bool Start()
    {
        if (IsRunning() || !AgentExists) return IsRunning();
        try
        {
            Process.Start(new ProcessStartInfo(AgentPath) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = AppContext.BaseDirectory });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Asks the agent to flush and exit; falls back to killing it if it does not comply.</summary>
    public async Task StopAsync()
    {
        if (!IsRunning()) return;
        try
        {
            using var stop = EventWaitHandle.OpenExisting(StopEventName);
            stop.Set();
        }
        catch
        {
            // Fall through to the process check below.
        }

        for (var i = 0; i < 20 && IsRunning(); i++) await Task.Delay(250);
        if (!IsRunning()) return;

        foreach (var process in Process.GetProcessesByName("FocusLensAgent"))
        {
            try { process.Kill(); } catch { /* already gone */ }
        }
    }

    public void SetPaused(bool paused) => SharedState.Update(s => s.TrackingPaused = paused);

    public bool IsAutoStartEnabled() => AutoStart.IsEnabled(AutoStartName);

    public void SetAutoStart(bool enabled)
    {
        if (enabled && AgentExists) AutoStart.Enable(AutoStartName, AgentPath);
        else AutoStart.Disable(AutoStartName);
    }
}
