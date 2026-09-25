using System.Text.Json;
using FocusLens.Core.Storage;

namespace FocusLens.Core.Ai.Chat;

/// <summary>A second, non-streaming model call that decides whether the answer deserves a chart.</summary>
public delegate Task<ChartPayload?> ChartProbe(string userPrompt, string prose, QueryContext context, CancellationToken ct);

public static class ChartProbes
{
    public static ChartProbe Default(StreamingAiClient client) => async (userPrompt, prose, context, ct) =>
    {
        var detail = string.Join("; ", context.AppDetails.Select(app =>
        {
            var items = string.Join(", ", app.Titles.Concat(app.Urls).Take(8).Select(i => $"{i.Label}={i.Seconds}"));
            return $"{app.Name}: [{items}]";
        }));
        var categories = string.Join(", ", context.CategoryTotals.Select(kv => $"{kv.Key}={kv.Value}"));
        var apps = string.Join(", ", context.TopApps.Select(a => $"{a.Name}={a.Seconds}"));

        var prompt = $$"""
            Decide whether a small chart would help visualise the assistant's answer. If yes, return ONLY JSON of the form:
              {"type":"bar|line|pie","points":[{"id":"x","label":"...","value":123.0,"color":"#1A56A0"}]}
            If no, return exactly the string: NONE

            Chart-type rules:
            - Use "bar" to compare apps, categories, or items (commands/files/sites). Default to this.
            - Use "line" ONLY for a trend across dates/time.
            - Use "pie" for share of a single whole (parts adding to 100%).
            - All points must use the SAME unit (seconds). label = the item name, value = seconds.
            - Give 3-8 points, each a distinct color hex. Never mix a "Time" axis label with item names.

            Use only data present below:
            CATEGORY BREAKDOWN: {{categories}}
            TOP APPS: {{apps}}
            APP ITEM DETAIL: {{(detail.Length == 0 ? "none" : detail)}}

            USER QUESTION: {{userPrompt}}
            ASSISTANT ANSWER: {{prose}}
            """;

        var raw = (await client.CompleteAsync(prompt, ct)).Trim();
        if (raw == "NONE") return null;

        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        try { return JsonSerializer.Deserialize<ChartPayload>(raw[start..(end + 1)], JsonFile.Options); }
        catch (JsonException) { return null; }
    };
}
