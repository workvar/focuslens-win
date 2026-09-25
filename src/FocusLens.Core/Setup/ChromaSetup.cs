using FocusLens.Core.Paths;

namespace FocusLens.Core.Setup;

/// <summary>
/// Sets up ChromaDB in a private Python virtual environment under the FocusLens data folder, so nothing is
/// installed into the user's own Python and removing FocusLens removes it too.
/// </summary>
public static class ChromaSetup
{
    public const string PythonDownloadPage = "https://www.python.org/downloads/windows/";

    /// <summary>Python 3.12 is the newest version chromadb ships wheels for on Windows, so it is what we install if none is found.</summary>
    private const string WingetPythonId = "Python.Python.3.12";

    public static string? FindPython()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        foreach (var root in new[] { Path.Combine(local, "Programs", "Python"), programs })
        {
            if (!Directory.Exists(root)) continue;
            foreach (var dir in Directory.EnumerateDirectories(root, "Python3*").OrderByDescending(d => d))
            {
                var exe = Path.Combine(dir, "python.exe");
                if (File.Exists(exe)) return exe;
            }
        }
        return ProcessRunner.FindOnPath("python.exe");
    }

    public static async Task InstallAsync(IProgress<SetupProgress> progress, CancellationToken ct)
    {
        var python = FindPython();
        if (python is null)
        {
            var winget = ProcessRunner.FindOnPath("winget.exe");
            if (winget is null)
                throw new SetupException($"Python 3 was not found and winget is not available. Install Python from {PythonDownloadPage}, then run setup again.");

            progress.Report(new SetupProgress(-1, "Installing Python 3.12 with winget..."));
            var exit = await ProcessRunner.RunAsync(winget, new[]
            {
                "install", "-e", "--id", WingetPythonId, "--scope", "user", "--silent",
                "--accept-package-agreements", "--accept-source-agreements",
            }, line => progress.Report(new SetupProgress(-1, $"Python: {line}")), ct);

            python = FindPython();
            if (python is null)
                throw new SetupException($"Python could not be installed automatically (exit {exit}). Install it from {PythonDownloadPage}, then run setup again.");
        }

        progress.Report(new SetupProgress(-1, "Creating a private Python environment..."));
        if (!File.Exists(AppPaths.ChromaPython))
        {
            var exit = await ProcessRunner.RunAsync(python, new[] { "-m", "venv", AppPaths.ChromaVenvDir }, null, ct);
            if (exit != 0 || !File.Exists(AppPaths.ChromaPython))
                throw new SetupException("Could not create the Python environment for Chroma.");
        }

        progress.Report(new SetupProgress(-1, "Installing chromadb. This downloads about 100 MB..."));
        var install = await ProcessRunner.RunAsync(
            AppPaths.ChromaPython,
            new[] { "-m", "pip", "install", "--upgrade", "--disable-pip-version-check", "pip", "chromadb" },
            line => progress.Report(new SetupProgress(-1, line.Length > 90 ? line[..90] + "..." : line)),
            ct);
        if (install != 0)
            throw new SetupException("pip could not install chromadb. Check your internet connection, or see the logs folder for details.");
    }
}
