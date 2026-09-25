using System.Diagnostics;
using FocusLens.Core.Ai;

namespace FocusLens.Core.Setup;

/// <summary>Finds, installs and starts Ollama, and pulls a first model. Every step is explicit and reports progress.</summary>
public static class OllamaSetup
{
    private const string InstallerUrl = "https://ollama.com/download/OllamaSetup.exe";
    public const string DownloadPage = "https://ollama.com/download/windows";
    public const string SuggestedModel = "llama3.2";

    public static string? FindExecutable()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        foreach (var candidate in new[]
        {
            Path.Combine(local, "Programs", "Ollama", "ollama.exe"),
            Path.Combine(programs, "Ollama", "ollama.exe"),
        })
            if (File.Exists(candidate)) return candidate;
        return ProcessRunner.FindOnPath("ollama.exe");
    }

    public static bool IsInstalled => FindExecutable() is not null;

    /// <summary>Downloads the official installer over HTTPS and runs it silently for the current user.</summary>
    public static async Task InstallAsync(IProgress<SetupProgress> progress, CancellationToken ct)
    {
        var installer = Path.Combine(Path.GetTempPath(), "OllamaSetup.exe");
        await ToolDownloader.DownloadAsync(InstallerUrl, installer, "Ollama", progress, ct);

        progress.Report(new SetupProgress(-1, "Installing Ollama. This can take a minute..."));
        var exit = await ProcessRunner.RunAsync(installer, new[] { "/SILENT", "/NORESTART" }, null, ct);
        try { File.Delete(installer); } catch { /* left in temp, harmless */ }

        if (exit != 0 || !IsInstalled)
            throw new SetupException("The Ollama installer did not finish. You can install it manually from ollama.com/download.");
    }

    /// <summary>Starts the Ollama server if it is not answering, and waits until it is (or gives up after 20 seconds).</summary>
    public static async Task<bool> EnsureRunningAsync(string host, CancellationToken ct)
    {
        var client = new OllamaClient(host);
        if (await client.IsRunningAsync(ct)) return true;

        var exe = FindExecutable();
        if (exe is null) return false;
        try
        {
            Process.Start(new ProcessStartInfo(exe, "serve") { UseShellExecute = false, CreateNoWindow = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }

        for (var i = 0; i < 40; i++)
        {
            await Task.Delay(500, ct);
            if (await client.IsRunningAsync(ct)) return true;
        }
        return false;
    }
}
