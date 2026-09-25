using FocusLens.Core.Paths;
using FocusLens.Core.Setup;

namespace FocusLens.Core.Chroma;

public enum ChromaIndexState
{
    NotInstalled,
    Idle,
    Indexing,
    Failed,
}

/// <summary>
/// Keeps the local Chroma index current. Indexing reads the SQLite database read-only and embeds on this PC, so
/// nothing leaves the machine. Only rows that are not indexed yet are embedded, which keeps repeat runs quick.
/// </summary>
public sealed class ChromaIndexService : IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan NotInstalledRecheck = TimeSpan.FromMinutes(1);

    private readonly ChromaSettings _settings = ChromaSettings.Load();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _loop;

    public ChromaIndexState State { get; private set; } = ChromaIndexState.Idle;
    public string Message { get; private set; } = "";
    public ChromaCounts Counts { get; private set; } = ChromaCounts.Empty;
    public DateTime? LastIndexedUtc => _settings.LastIndexedUtc;

    /// <summary>Raised from a background thread whenever state, counts or the progress line change.</summary>
    public event Action? Changed;

    /// <summary>Read by chat on every question, so it stays a plain field read.</summary>
    public static bool SemanticSearchEnabled { get; private set; } = ChromaSettings.Load().Enabled;

    public bool Enabled
    {
        get => _settings.Enabled;
        set
        {
            _settings.Enabled = value;
            SemanticSearchEnabled = value;
            _settings.Save();
            if (value) Start(); else Stop();
            Changed?.Invoke();
        }
    }

    // MARK: status

    public async Task RefreshStatusAsync(CancellationToken ct = default)
    {
        if (!File.Exists(AppPaths.ChromaPython)) { Set(ChromaIndexState.NotInstalled, ""); return; }
        if (State == ChromaIndexState.NotInstalled) Set(ChromaIndexState.Idle, "");
        var counts = await ChromaSearch.CountsAsync(ct);
        if (counts is not null) { Counts = counts; Changed?.Invoke(); }
    }

    // MARK: indexing

    /// <summary>"full" rebuilds every collection from scratch; otherwise only new rows are embedded.</summary>
    public async Task ReindexAsync(bool full, CancellationToken ct = default)
    {
        if (!await _gate.WaitAsync(0, ct)) return; // already indexing
        try
        {
            if (!File.Exists(AppPaths.ChromaPython)) { Set(ChromaIndexState.NotInstalled, ""); return; }
            if (!File.Exists(AppPaths.DatabaseFile)) { Set(ChromaIndexState.Failed, "No activity has been recorded yet."); return; }
            if (!ChromaScripts.Stage()) { Set(ChromaIndexState.Failed, "The index scripts are missing from the app."); return; }

            Set(ChromaIndexState.Indexing, "");
            var args = new List<string>
            {
                ChromaScripts.PathOf("ingest.py"), "--db", AppPaths.DatabaseFile, "--out", AppPaths.ChromaDir,
                full ? "--reset" : "--skip-existing",
            };
            var lastLine = "";
            var exit = await ProcessRunner.RunAsync(AppPaths.ChromaPython, args, line =>
            {
                lastLine = line;
                Message = line;
                Changed?.Invoke();
            }, ct);

            if (exit != 0) { Set(ChromaIndexState.Failed, Truncate(lastLine, "Indexing stopped unexpectedly.")); return; }

            _settings.LastIndexedUtc = DateTime.UtcNow;
            _settings.Save();
            Set(ChromaIndexState.Idle, "");
            await RefreshStatusAsync(ct);
        }
        catch (OperationCanceledException)
        {
            Set(ChromaIndexState.Idle, "");
        }
        catch (SetupException ex)
        {
            Set(ChromaIndexState.Failed, ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync()
    {
        if (!await _gate.WaitAsync(0)) return;
        try
        {
            if (Directory.Exists(AppPaths.ChromaDir)) Directory.Delete(AppPaths.ChromaDir, recursive: true);
            _settings.LastIndexedUtc = null;
            _settings.Save();
            Counts = ChromaCounts.Empty;
            if (State != ChromaIndexState.NotInstalled) Set(ChromaIndexState.Idle, "");
            else Changed?.Invoke();
        }
        catch (IOException ex)
        {
            Set(ChromaIndexState.Failed, $"Could not delete the index: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    // MARK: background refresh

    public void Start()
    {
        if (_loop is not null || !Enabled) return;
        var cts = _loop = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                var installed = File.Exists(AppPaths.ChromaPython);
                if (installed) await ReindexAsync(full: false, cts.Token);
                // Until Chroma is installed, look again soon so indexing starts right after setup finishes.
                try { await Task.Delay(installed ? RefreshInterval : NotInstalledRecheck, cts.Token); }
                catch (OperationCanceledException) { return; }
            }
        });
    }

    public void Stop()
    {
        _loop?.Cancel();
        _loop = null;
    }

    public void Dispose() => Stop();

    private void Set(ChromaIndexState state, string message)
    {
        State = state;
        Message = message;
        Changed?.Invoke();
    }

    private static string Truncate(string line, string fallback) =>
        string.IsNullOrWhiteSpace(line) ? fallback : line.Length > 140 ? line[..140] : line;
}
