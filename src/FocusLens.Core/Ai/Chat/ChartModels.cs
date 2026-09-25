using System.Text.Json;
using FocusLens.Core.Storage;

namespace FocusLens.Core.Ai.Chat;

public enum ChartType
{
    Bar,
    Line,
    Pie,
    None,
}

public sealed record ChartDataPoint(string Id, string Label, double Value, string Color);

public sealed record ChartPayload(ChartType Type, IReadOnlyList<ChartDataPoint> Points)
{
    public string ToJson() => JsonSerializer.Serialize(this, JsonFile.Options);

    public static ChartPayload? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<ChartPayload>(json, JsonFile.Options); }
        catch (JsonException) { return null; }
    }
}

/// <summary>An event from a streaming chat answer.</summary>
public abstract record StreamEvent
{
    public sealed record Token(string Text) : StreamEvent;
    public sealed record Done(ChartPayload? Chart) : StreamEvent;
    public sealed record Error(Exception Exception) : StreamEvent;
}
