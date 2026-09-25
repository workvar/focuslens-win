using System.Text.Json;
using FocusLens.Core.Paths;

namespace FocusLens.Core.Health;

/// <summary>
/// Checks the local ChromaDB search index: Python is installed, the chromadb package imports, and the
/// persistent store under %LOCALAPPDATA%\FocusLens\chroma opens. Read-only; it never creates the store.
/// </summary>
public sealed class ChromaProbe : IHealthProbe
{
    /// <summary>Prints one JSON line. Telemetry is switched off so a health check never phones home.</summary>
    private const string Script = """
        import json, os, sys
        try:
            import chromadb
        except Exception:
            print(json.dumps({"package": False}))
            sys.exit(0)
        path = os.environ["FOCUSLENS_CHROMA_PATH"]
        info = {"package": True, "version": chromadb.__version__, "store": os.path.exists(os.path.join(path, "chroma.sqlite3"))}
        if info["store"]:
            client = chromadb.PersistentClient(path=path)
            info["collections"] = len(client.list_collections())
        print(json.dumps(info))
        """;

    public string Name => "Chroma DB";

    /// <summary>Importing chromadb takes a few seconds, so this is the slowest probe by far.</summary>
    public TimeSpan MinInterval => TimeSpan.FromMinutes(1);

    public async Task<ServiceHealth> CheckAsync(CancellationToken ct)
    {
        var env = new Dictionary<string, string>
        {
            ["FOCUSLENS_CHROMA_PATH"] = AppPaths.ChromaDir,
            ["ANONYMIZED_TELEMETRY"] = "False",
        };

        var run = await PythonRunner.RunAsync(Script, env, ct);
        if (!run.Started)
            return ServiceHealth.Disconnected(Name, "Python 3 was not found. Install it from python.org, then run: pip install chromadb");
        if (run.ExitCode != 0 || !TryParse(run.Output, out var info))
            return ServiceHealth.Disconnected(Name, "The Chroma store could not be opened. It may be locked or damaged");

        if (!info.GetProperty("package").GetBoolean())
            return ServiceHealth.Disconnected(Name, "The chromadb package is missing. Run: pip install chromadb");

        var version = info.GetProperty("version").GetString();
        if (!info.GetProperty("store").GetBoolean())
            return ServiceHealth.Degraded(Name, $"chromadb {version} is installed, but no search index has been created yet");

        var collections = info.GetProperty("collections").GetInt32();
        return ServiceHealth.Connected(Name, $"chromadb {version}, {collections} collections");
    }

    private static bool TryParse(string output, out JsonElement info)
    {
        info = default;
        try
        {
            // Some packages print warnings first; the payload is always the last line.
            var line = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
            if (line is null) return false;
            info = JsonDocument.Parse(line).RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
