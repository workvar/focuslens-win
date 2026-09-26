using System.Text.Json;

namespace FocusLens.Core.Guide;

/// <summary>
/// Turns the model's reply into steps. Models wrap JSON in fences and prose, or answer with a
/// bare array, so the parser finds the outermost JSON and accepts both. An unusable reply is an
/// error, never a guess: a made-up step is worse than saying "try again".
/// </summary>
public static class GuidePlanParser
{
    public const int MaxSteps = 8;

    private sealed record Flat(string? Title, string? Detail, string? Action, string? Role, string? Label, string? Area, string? Text);
    private sealed record Envelope(List<Flat>? Steps);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
    };

    public static IReadOnlyList<GuideStep> Parse(string reply)
    {
        var json = ExtractJson(reply) ?? throw new FormatException("The reply had no JSON.");

        List<Flat>? flats;
        try
        {
            flats = json.TrimStart().StartsWith('[')
                ? JsonSerializer.Deserialize<List<Flat>>(json, Options)
                : JsonSerializer.Deserialize<Envelope>(json, Options)?.Steps;
        }
        catch (JsonException ex)
        {
            throw new FormatException("The reply was not a valid plan.", ex);
        }

        var steps = (flats ?? new List<Flat>()).Take(MaxSteps).Select(MakeStep).OfType<GuideStep>().ToList();
        if (steps.Count == 0) throw new FormatException("The plan had no steps.");
        return steps;
    }

    private static GuideStep? MakeStep(Flat flat)
    {
        var title = flat.Title?.Trim();
        if (string.IsNullOrEmpty(title)) return null;
        var action = Enum.TryParse<GuideAction>(flat.Action ?? "click", ignoreCase: true, out var parsed) ? parsed : GuideAction.Click;
        var label = flat.Label?.Trim();
        var target = string.IsNullOrEmpty(label) ? null : new GuideTarget(label, flat.Role, flat.Area);
        var typed = flat.Text?.Trim();
        return new GuideStep(title, flat.Detail, action, target, string.IsNullOrEmpty(typed) ? null : typed);
    }

    /// <summary>The text from the first '{' or '[' to its last matching closer.</summary>
    public static string? ExtractJson(string text)
    {
        var start = text.IndexOfAny(new[] { '{', '[' });
        if (start < 0) return null;
        var closer = text[start] == '{' ? '}' : ']';
        var end = text.LastIndexOf(closer);
        return end > start ? text[start..(end + 1)] : null;
    }
}
