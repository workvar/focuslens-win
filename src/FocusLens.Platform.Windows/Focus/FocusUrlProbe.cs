using FocusLens.Platform.Windows.Capture;

namespace FocusLens.Platform.Windows.Focus;

/// <summary>
/// Reads a browser's address bar through UI Automation on a background thread and gives up
/// after <see cref="Timeout"/>. Only one read runs at a time: while a slow one is still going,
/// the next call reports a timeout instead of piling up more.
///
/// UI Automation sometimes returns no URL for a moment (while typing in the address bar, or
/// mid-navigation), which would turn "youtube.com" into "Chrome" and look like the user changed
/// site. So the last URL is reused while the page title is unchanged, or for three seconds.
/// </summary>
internal sealed class FocusUrlProbe
{
    /// <summary>Busy browsers regularly need more than a quarter of a second. Off the UI thread, so a generous limit is free.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1.5);

    private readonly BrowserUrlReader _reader = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private (uint Pid, string Title, string Url, DateTime At)? _lastUrl;

    public readonly record struct Result(string? Url, bool TimedOut);

    public async Task<Result> ReadAsync(IntPtr hwnd, uint pid, string title)
    {
        if (!await _gate.WaitAsync(0)) return new Result(Fallback(pid, title), TimedOut: true);

        var read = Task.Run(() =>
        {
            try { return _reader.ReadUrl(hwnd); }
            finally { _gate.Release(); }
        });

        try
        {
            var url = await read.WaitAsync(Timeout);
            if (url is not null)
            {
                _lastUrl = (pid, title, url, DateTime.UtcNow);
                return new Result(url, TimedOut: false);
            }
            return new Result(Fallback(pid, title), TimedOut: false);
        }
        catch (TimeoutException)
        {
            return new Result(Fallback(pid, title), TimedOut: true);
        }
        catch
        {
            return new Result(null, TimedOut: false);
        }
    }

    /// <summary>A missed read: trust the last URL if the page looks the same, or if it was read a moment ago.</summary>
    private string? Fallback(uint pid, string title)
    {
        if (_lastUrl is not { } last || last.Pid != pid) return null;
        return last.Title == title || DateTime.UtcNow - last.At < TimeSpan.FromSeconds(3) ? last.Url : null;
    }
}
