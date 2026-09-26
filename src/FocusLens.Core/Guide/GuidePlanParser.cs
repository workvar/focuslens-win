using System.Text.Json;

namespace FocusLens.Core.Guide;

/// <summary>
/// Turns the model's reply into the next few steps. Models wrap JSON in fences and prose, or answer
/// with a bare array, so the parser finds the plan object and accepts both. An unusable reply is an
/// error, never a guess. "done" and "blocked" may have no steps; "continue" must have some.
/// </summary>
public static class GuidePlanParser
{
    public const int MaxSteps = GuidePrompt.MaxPlannedSteps;

    private sealed record Flat(string? Title, string? Detail, string? Action, string? Role, string? Label, string? Area, string? Text);
    private sealed record Envelope(string? Status, string? Note, List<Flat>? Steps);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
    };

    public static GuidePlan Parse(string reply)
    {
        var json = ExtractJson(reply) ?? throw new FormatException("The reply had no JSON.");

        try
        {
            if (json.TrimStart().StartsWith('['))
            {
                var array = JsonSerializer.Deserialize<List<Flat>>(json, Options) ?? new List<Flat>();
                return MakePlan(null, null, array);
            }
            var envelope = JsonSerializer.Deserialize<Envelope>(json, Options)
                ?? throw new FormatException("The reply was not a valid plan.");
            return MakePlan(envelope.Status, envelope.Note, envelope.Steps ?? new List<Flat>());
        }
        catch (JsonException ex)
        {
            throw new FormatException("The reply was not a valid plan.", ex);
        }
    }

    private static GuidePlan MakePlan(string? status, string? note, List<Flat> flats)
    {
        var steps = flats.Take(MaxSteps).Select(MakeStep).OfType<GuideStep>().ToList();
        var clean = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return StatusOf(status) switch
        {
            GuidePlanStatus.Done => new GuidePlan(GuidePlanStatus.Done, steps, clean),
            GuidePlanStatus.Blocked => new GuidePlan(GuidePlanStatus.Blocked, steps, clean),
            _ when steps.Count == 0 => throw new FormatException("The plan had no steps."),
            _ => new GuidePlan(GuidePlanStatus.Proceed, steps, clean),
        };
    }

    /// <summary>A missing or unknown status is a normal "keep going" plan, which is what older replies were.</summary>
    private static GuidePlanStatus StatusOf(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "done" => GuidePlanStatus.Done,
        "blocked" => GuidePlanStatus.Blocked,
        _ => GuidePlanStatus.Proceed,
    };

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

    /// <summary>
    /// The plan object, found by "status" or "steps" sitting just after a '{', so a reply that starts
    /// with "status" is not sliced open at "steps". Otherwise the first '{' or '[' to its last closer.
    /// </summary>
    public static string? ExtractJson(string text)
    {
        var plan = ObjectContainingPlan(text);
        if (plan is not null) return plan;
        var start = text.IndexOfAny(new[] { '{', '[' });
        if (start < 0) return null;
        var closer = text[start] == '{' ? '}' : ']';
        var end = text.LastIndexOf(closer);
        return end > start ? text[start..(end + 1)] : null;
    }

    private static string? ObjectContainingPlan(string text)
    {
        var search = 0;
        while (search < text.Length)
        {
            var statusAt = text.IndexOf("\"status\"", search, StringComparison.Ordinal);
            var stepsAt = text.IndexOf("\"steps\"", search, StringComparison.Ordinal);
            var keyAt = Earliest(statusAt, stepsAt);
            if (keyAt < 0) return null;
            var start = text.LastIndexOf('{', keyAt);
            var end = text.LastIndexOf('}');
            if (start >= 0 && end > start && GapIsWhitespace(text, start + 1, keyAt))
                return text[start..(end + 1)];
            search = keyAt + 1;
        }
        return null;
    }

    private static int Earliest(int a, int b)
    {
        if (a < 0) return b;
        if (b < 0) return a;
        return Math.Min(a, b);
    }

    private static bool GapIsWhitespace(string text, int from, int to)
    {
        for (var i = from; i < to; i++)
            if (!char.IsWhiteSpace(text[i])) return false;
        return true;
    }
}
