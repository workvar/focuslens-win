using FocusLens.Core.Paths;

namespace FocusLens.Core.Logging;

/// <summary>Minimal rolling file logger shared by the agent and the app. Never throws.</summary>
public sealed class FileLog
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private readonly object _gate = new();
    private readonly string _path;

    public FileLog(string name) => _path = Path.Combine(AppPaths.LogsDir, name + ".log");

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message, Exception? ex = null) => Write("ERROR", ex is null ? message : $"{message}: {ex}");

    private void Write(string level, string message)
    {
        try
        {
            lock (_gate)
            {
                Roll();
                File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never break tracking.
        }
    }

    private void Roll()
    {
        var info = new FileInfo(_path);
        if (!info.Exists || info.Length < MaxBytes) return;
        var old = _path + ".1";
        if (File.Exists(old)) File.Delete(old);
        File.Move(_path, old);
    }
}
