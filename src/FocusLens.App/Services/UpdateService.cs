using Velopack;
using Velopack.Sources;

namespace FocusLens.App.Services;

/// <summary>
/// Checks GitHub Releases for a newer version. Checking never downloads: the user is told about the
/// release first and calls <see cref="DownloadAsync"/> when they agree, then <see cref="ApplyAndRestart"/>.
/// Does nothing when running from a dev build.
/// </summary>
public sealed class UpdateService : IDisposable
{
    private const string RepoUrl = "https://github.com/workvar/focuslens-win";
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(4);

    private readonly UpdateManager _manager = new(new GithubSource(RepoUrl, null, false));
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Action<string, Exception> _logError;
    private CancellationTokenSource? _cts;
    private UpdateInfo? _info;
    private VelopackAsset? _ready;

    public UpdateService(Action<string, Exception> logError) => _logError = logError;

    /// <summary>Raised on every state change, possibly from a background thread.</summary>
    public event Action<UpdateState>? StateChanged;

    public UpdateState State { get; private set; } = new(UpdateStatus.Idle);
    public bool IsInstalled => _manager.IsInstalled;
    public string CurrentVersion => _manager.CurrentVersion?.ToString() ?? DevVersion;

    private static string DevVersion =>
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "dev";

    public void Start()
    {
        if (!IsInstalled || _cts is not null) return;
        _cts = new CancellationTokenSource();
        _ = RunLoopAsync(_cts.Token);
    }

    /// <summary>Looks for a newer release. Leaves the state at Available, UpToDate or Failed.</summary>
    public async Task CheckAsync()
    {
        if (!IsInstalled || State.Status is UpdateStatus.Available or UpdateStatus.Downloading or UpdateStatus.Ready) return;

        await _gate.WaitAsync();
        try
        {
            SetState(new(UpdateStatus.Checking));
            var info = await _manager.CheckForUpdatesAsync();
            if (info is null)
            {
                SetState(new(UpdateStatus.UpToDate));
                return;
            }

            _info = info;
            var target = info.TargetFullRelease;
            SetState(new(UpdateStatus.Available, target.Version.ToString(), target.NotesMarkdown));
        }
        catch (Exception ex)
        {
            _logError("Update check failed", ex);
            SetState(new(UpdateStatus.Failed, Error: "Could not reach GitHub. Check your connection and try again."));
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Downloads the release found by <see cref="CheckAsync"/>. Call only after the user agreed.</summary>
    public async Task DownloadAsync()
    {
        var info = _info;
        if (info is null || State.Status != UpdateStatus.Available) return;

        var target = info.TargetFullRelease;
        var version = target.Version.ToString();
        var notes = target.NotesMarkdown;

        await _gate.WaitAsync();
        try
        {
            SetState(new(UpdateStatus.Downloading, version, notes));
            await _manager.DownloadUpdatesAsync(info, percent => SetState(new(UpdateStatus.Downloading, version, notes, percent)));
            _ready = target;
            SetState(new(UpdateStatus.Ready, version, notes, 100));
        }
        catch (Exception ex)
        {
            _logError("Update download failed", ex);
            SetState(new(UpdateStatus.Available, version, notes, Error: "Download failed. Check your connection and try again."));
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Applies the downloaded update and relaunches the app. Does not return on success.</summary>
    public void ApplyAndRestart()
    {
        if (_ready is not null) _manager.ApplyUpdatesAndRestart(_ready);
    }

    /// <summary>Applies a downloaded update after this process exits, without relaunching.</summary>
    public void ApplyOnExit()
    {
        if (_ready is not null) _manager.WaitExitThenApplyUpdates(_ready, silent: true, restart: false);
    }

    private void SetState(UpdateState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }

    /// <summary>Checks periodically until the user has been told about a release.</summary>
    private async Task RunLoopAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(StartupDelay, token);
            while (!token.IsCancellationRequested && State.Status is not (UpdateStatus.Available or UpdateStatus.Downloading or UpdateStatus.Ready))
            {
                await CheckAsync();
                await Task.Delay(CheckInterval, token);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    public void Dispose() => _cts?.Cancel();
}
