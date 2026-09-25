namespace FocusLens.Core.Models;

public sealed class ScreenshotRecord
{
    public long? Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string AppBundleId { get; set; } = "";
    public string AppName { get; set; } = "";
    public string? WindowTitle { get; set; }
    public string? OcrText { get; set; }
    public string? ThumbPath { get; set; }
    public string FocusState { get; set; } = "foreground";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed record BrowserTab(string Browser, string Title, string Url, string AppId);

public sealed record OpenWindow(IntPtr Handle, uint ProcessId, string AppId, string AppName, string? Title);

public sealed record CaptureContext(string AppId, string AppName, string? WindowTitle, string? Url);
