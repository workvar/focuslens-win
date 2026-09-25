namespace FocusLens.Core.Models;

/// <summary>
/// One second of tracked activity. On Windows AppBundleId holds the executable
/// name (for example "code.exe"); the column name matches the macOS schema.
/// </summary>
public sealed class ActivityEvent
{
    public long? Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string AppBundleId { get; set; } = "";
    public string AppName { get; set; } = "";
    public string? WindowTitle { get; set; }
    public string? Url { get; set; }
    public bool IsIdle { get; set; }
    public long? CategoryId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
