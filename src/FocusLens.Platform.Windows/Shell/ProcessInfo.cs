using System.Diagnostics;
using FocusLens.Core.Meetings.Detection;
using FocusLens.Platform.Windows.Native;

namespace FocusLens.Platform.Windows.Shell;

/// <summary>Executable identity for processes. The "app id" is the lowercase exe file name.</summary>
public static class ProcessInfo
{
    public static string? AppIdOf(uint pid)
    {
        var path = Kernel32.ImagePath(pid);
        return path is null ? null : Path.GetFileName(path).ToLowerInvariant();
    }

    /// <summary>A friendly name: the exe's FileDescription, falling back to the exe name.</summary>
    public static string AppNameOf(uint pid, string fallback)
    {
        try
        {
            var path = Kernel32.ImagePath(pid);
            if (path is null) return fallback;
            var description = FileVersionInfo.GetVersionInfo(path).FileDescription;
            return string.IsNullOrWhiteSpace(description)
                ? Path.GetFileNameWithoutExtension(path)
                : description.Trim();
        }
        catch
        {
            return fallback;
        }
    }

    /// <summary>
    /// Walks up while the parent runs the same executable, so a browser's audio or GPU
    /// helper maps back to the main browser process.
    /// </summary>
    public static int OwningPid(int pid)
    {
        var table = Kernel32.ProcessTable();
        var current = (uint)pid;
        for (var depth = 0; depth < 8; depth++)
        {
            if (!table.TryGetValue(current, out var self)) break;
            if (!table.TryGetValue(self.Parent, out var parent)) break;
            if (!string.Equals(parent.Exe, self.Exe, StringComparison.OrdinalIgnoreCase)) break;
            current = self.Parent;
        }
        return (int)current;
    }

    public static bool IsRunning(int pid)
    {
        try { return !Process.GetProcessById(pid).HasExited; }
        catch { return false; }
    }
}

public sealed class WindowsProcessInspector : IProcessInspector
{
    public string? AppIdOf(int pid) => ProcessInfo.AppIdOf((uint)pid);
    public int OwningPid(int pid) => ProcessInfo.OwningPid(pid);
    public bool IsRunning(int pid) => ProcessInfo.IsRunning(pid);
}
