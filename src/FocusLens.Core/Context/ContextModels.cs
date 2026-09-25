namespace FocusLens.Core.Context;

public sealed record ScreenTextSnapshot(
    DateTime Timestamp, string AppId, string AppName, string? Title, string Text, bool IsBackground);

public sealed record SessionNote(
    string AppName, string? Title, string? Url, DateTime Start, DateTime End,
    int SnapshotCount, IReadOnlyList<string> NewLines, bool IsBackground);

public sealed record OpenTab(string Browser, string Title, string Url, DateTime Seen);

public sealed record InputTotal(string AppName, int Keys, int Clicks, int Scrolls);
public sealed record DocumentVisit(string AppName, string Path, DateTime LastSeen);
public sealed record SystemEventRecord(DateTime Timestamp, string Kind, string? Detail);

public sealed class ActivitySignals
{
    public List<InputTotal> Input { get; } = new();
    public List<DocumentVisit> Documents { get; } = new();
    public List<SystemEventRecord> Events { get; } = new();
    public Dictionary<string, int> CopiesByApp { get; } = new();

    public bool IsEmpty => Input.Count == 0 && Documents.Count == 0 && Events.Count == 0 && CopiesByApp.Count == 0;
}
