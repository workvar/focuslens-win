using Velopack;
using Velopack.Sources;

namespace FocusLens.App.Services;

/// <summary>
/// Checks GitHub Releases for a newer version, downloads it in the background, and applies it
/// when the user restarts (or quits). Does nothing when running from a dev build.
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
    private VelopackAsset? _ready;

    public UpdateService(Action<string, Exception> logError) => _logError = logError;

    /// <summary>Raised with the new version once it is downloaded and ready to apply.</summary>
    public event Action<string>? UpdateReady;

    public bool IsInstalled => _manager.IsInstalled;
    public string? ReadyVersion => _ready?.Version.ToString();

    public void Start()
    {
        if (!IsInstalled || _cts is not null) return;
        _cts = new CancellationTokenSource();
        _ = RunLoopAsync(_cts.Token);
    }

    /// <summary>Returns true when an update is downloaded and ready (now or from an earlier check).</summary>
    public async Task<bool> CheckAsync()
    {
        if (!IsInstalled) return false;
        if (_ready is not null) return true;

        await _gate.WaitAsync();
        try
        {
            var info = await _manager.CheckForUpdatesAsync();
            if (info is null) return false;

            await _manager.DownloadUpdatesAsync(info);
            _ready = info.TargetFullRelease;
            UpdateReady?.Invoke(_ready.Version.ToString());
            return true;
        }
        catch (Exception ex)
        {
            _logError("Update check failed", ex);
            return false;
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

    /// <summary>Applies the downloaded update after this process exits, without relaunching.</summary>
    public void ApplyOnExit()
    {
        if (_ready is not null) _manager.WaitExitThenApplyUpdates(_ready, silent: true, restart: false);
    }

    private async Task RunLoopAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(StartupDelay, token);
            while (!token.IsCancellationRequested && _ready is null)
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
