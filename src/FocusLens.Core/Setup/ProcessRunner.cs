using System.Diagnostics;

namespace FocusLens.Core.Setup;

/// <summary>Runs an external installer or tool without a console window, streaming each output line to a callback.</summary>
public static class ProcessRunner
{
    public static async Task<int> RunAsync(
        string exe, IEnumerable<string> args, Action<string>? onLine, CancellationToken ct, IDictionary<string, string>? env = null)
    {
        var info = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        info.Environment["PYTHONIOENCODING"] = "utf-8";
        if (env is not null) foreach (var (key, value) in env) info.Environment[key] = value;

        using var process = new Process { StartInfo = info };
        void Handle(object _, DataReceivedEventArgs e) { if (!string.IsNullOrWhiteSpace(e.Data)) onLine?.Invoke(e.Data); }
        process.OutputDataReceived += Handle;
        process.ErrorDataReceived += Handle;

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(ct);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
            throw;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new SetupException($"Could not start {Path.GetFileName(exe)}.", ex);
        }
    }

    /// <summary>First match for an executable name on PATH, ignoring the Microsoft Store's python.exe placeholders.</summary>
    public static string? FindOnPath(string exeName)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim('"'), exeName);
                if (File.Exists(candidate) && !candidate.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase)) return candidate;
            }
            catch (ArgumentException) { /* malformed PATH entry */ }
        }
        return null;
    }
}
