using System.Text.Json;
using FocusLens.Core.Storage;

namespace FocusLens.Core.Models;

public sealed record CategoryTotal(string Id, string Name, string ColorHex, int Seconds)
{
    public double Hours => Seconds / 3600.0;
    public int Minutes => Seconds / 60;
}

public sealed record AppTotal(string Id, string BundleId, string Name, int Seconds)
{
    public double Hours => Seconds / 3600.0;
    public int Minutes => Seconds / 60;
}

/// <summary>Pre-computed aggregate for a single calendar day.</summary>
public sealed class DailySummary
{
    public long? Id { get; set; }
    public string Date { get; set; } = "";
    public int TotalActiveS { get; set; }
    public int TotalIdleS { get; set; }
    public double FocusScore { get; set; }
    public string CategoryJson { get; set; } = "[]";
    public string TopAppsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public int TotalRecordedS => TotalActiveS + TotalIdleS;

    public IReadOnlyList<CategoryTotal> CategoryTotals => Decode<CategoryTotal>(CategoryJson);
    public IReadOnlyList<AppTotal> TopApps => Decode<AppTotal>(TopAppsJson);

    public string FocusLabel => FocusScore switch
    {
        >= 80 => "Excellent",
        >= 60 => "Good",
        >= 40 => "Average",
        >= 20 => "Low",
        _ => "Very Low",
    };

    private static IReadOnlyList<T> Decode<T>(string json)
    {
        try { return JsonSerializer.Deserialize<List<T>>(json, JsonFile.Options) ?? new List<T>(); }
        catch { return new List<T>(); }
    }
}
