using System.Text;
using System.Text.Json;
using FocusLens.Core.Paths;
using FocusLens.Core.Setup;

namespace FocusLens.Core.Chroma;

public sealed record ChromaHit(string Source, double Score, string Text, string Label, string When);

public sealed record ChromaCounts(IReadOnlyDictionary<string, int> BySource)
{
    public static readonly ChromaCounts Empty = new(new Dictionary<string, int>());
    public int Total => BySource.Values.Sum();
    public int Of(string source) => BySource.TryGetValue(source, out var n) ? n : 0;
}

/// <summary>
/// Semantic search over the local Chroma index. Runs search.py in the private environment and turns its JSON into
/// prompt text. Every failure returns "nothing found", so chat behaves exactly as it did before the index existed.
/// </summary>
public static class ChromaSearch
{
    public const int HitsPerCollection = 4;
    private const int MaxRenderedCharacters = 3000;

    public static bool IsReady => File.Exists(AppPaths.ChromaPython) && Directory.Exists(AppPaths.ChromaDir);

    public static async Task<IReadOnlyList<ChromaHit>> SearchAsync(string question, CancellationToken ct = default)
    {
        if (!IsReady || string.IsNullOrWhiteSpace(question)) return Array.Empty<ChromaHit>();
        var json = await RunAsync(new[] { "-n", HitsPerCollection.ToString(), "--", question }, ct);
        return json is null ? Array.Empty<ChromaHit>() : ParseHits(json.Value);
    }

    public static async Task<ChromaCounts?> CountsAsync(CancellationToken ct = default)
    {
        if (!IsReady) return null;
        var json = await RunAsync(new[] { "--stats" }, ct);
        if (json is null || !json.Value.TryGetProperty("counts", out var counts)) return null;
        return new ChromaCounts(counts.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt32()));
    }

    /// <summary>Prompt text for the model, best matches first, capped so a local model's context stays small.</summary>
    public static string Render(IEnumerable<ChromaHit> hits)
    {
        var lines = new StringBuilder();
        foreach (var hit in hits)
        {
            var head = string.Join(" | ", new[] { hit.Source, hit.Label, hit.When }.Where(s => s.Length > 0));
            var line = $"  - [{head}] {hit.Text.Replace('\n', ' ').Replace('\r', ' ')}";
            if (lines.Length + line.Length > MaxRenderedCharacters) break;
            lines.AppendLine(line);
        }
        return lines.ToString().TrimEnd();
    }

    private static async Task<JsonElement?> RunAsync(string[] extraArgs, CancellationToken ct)
    {
        var args = new List<string> { ChromaScripts.PathOf("search.py"), "--out", AppPaths.ChromaDir };
        // search.py takes options before the query; "--" keeps a question that starts with a dash from being read as one.
        args.InsertRange(args.Count, extraArgs);

        var lines = new List<string>();
        try
        {
            var exit = await ProcessRunner.RunAsync(AppPaths.ChromaPython, args, line => { lock (lines) lines.Add(line); }, ct);
            if (exit != 0) return null;
            string? last;
            lock (lines) last = lines.LastOrDefault(l => l.StartsWith('{'));
            return last is null ? null : JsonDocument.Parse(last).RootElement.Clone();
        }
        catch (Exception ex) when (ex is SetupException or JsonException or IOException)
        {
            return null;
        }
    }

    private static IReadOnlyList<ChromaHit> ParseHits(JsonElement root)
    {
        if (!root.TryGetProperty("hits", out var hits)) return Array.Empty<ChromaHit>();
        var result = new List<ChromaHit>();
        foreach (var hit in hits.EnumerateArray())
        {
            var text = Str(hit, "text");
            if (text.Length == 0) continue;
            var meta = hit.TryGetProperty("meta", out var m) ? m : default;
            var label = Str(meta, "app_name");
            if (label.Length == 0) label = Str(meta, "conversation_title");
            var score = hit.TryGetProperty("score", out var s) ? s.GetDouble() : 0;
            result.Add(new ChromaHit(Str(hit, "source"), score, text, label, Str(meta, "date")));
        }
        return result;
    }

    private static string Str(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? "" : "";
}
