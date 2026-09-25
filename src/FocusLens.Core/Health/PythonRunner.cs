using System.Diagnostics;
using FocusLens.Core.Paths;

namespace FocusLens.Core.Health;

public sealed record PythonResult(bool Started, int ExitCode, string Output);

/// <summary>Runs a short inline Python script with whichever interpreter answers first.</summary>
public static class PythonRunner
{
    private const int StorePlaceholderExitCode = 9009;

    /// <summary>The Windows launcher first, then the names an installer or the Store may register.</summary>
    private static readonly (string Exe, string[] Prefix)[] SystemCandidates =
    {
        ("py", new[] { "-3" }),
        ("python", Array.Empty<string>()),
        ("python3", Array.Empty<string>()),
    };

    /// <summary>The private environment FocusLens sets up for Chroma wins over whatever is on PATH.</summary>
    private static IEnumerable<(string Exe, string[] Prefix)> Candidates()
    {
        if (File.Exists(AppPaths.ChromaPython)) yield return (AppPaths.ChromaPython, Array.Empty<string>());
        foreach (var candidate in SystemCandidates) yield return candidate;
    }

    /// <summary>
    /// Tries each interpreter in turn and returns the first one that exits cleanly. When none does, returns
    /// the last result that at least started, so callers can tell "no Python" from "script failed".
    /// </summary>
    public static async Task<PythonResult> RunAsync(
        string script, IReadOnlyDictionary<string, string> env, CancellationToken ct)
    {
        PythonResult best = new(false, -1, "");
        foreach (var (exe, prefix) in Candidates())
        {
            var result = await TryRunAsync(exe, prefix, script, env, ct);
            if (result.Started && result.ExitCode == 0) return result;
            if (result.Started && !best.Started) best = result;
        }
        return best;
    }

    private static async Task<PythonResult> TryRunAsync(
        string exe, string[] prefix, string script, IReadOnlyDictionary<string, string> env, CancellationToken ct)
    {
        var info = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in prefix) info.ArgumentList.Add(arg);
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add(script);
        info.Environment["PYTHONIOENCODING"] = "utf-8";
        foreach (var (key, value) in env) info.Environment[key] = value;

        Process process;
        try
        {
            process = Process.Start(info)!;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new PythonResult(false, -1, ""); // this interpreter name does not exist
        }

        using (process)
        {
            try
            {
                var output = process.StandardOutput.ReadToEndAsync(ct);
                _ = process.StandardError.ReadToEndAsync(ct); // drained so the child never blocks on a full pipe
                await process.WaitForExitAsync(ct);
                // 9009 is what the Microsoft Store's python.exe placeholder returns when Python is not installed.
                if (process.ExitCode == StorePlaceholderExitCode) return new PythonResult(false, -1, "");
                return new PythonResult(true, process.ExitCode, await output);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
                throw;
            }
        }
    }
}
