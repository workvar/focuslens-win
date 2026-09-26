using System.Text.Json;

namespace FocusLens.Core.Guide;

/// <summary>
/// Turns the model's reply into the next few steps. Models wrap JSON in fences and prose, add a
/// trailing comma, or answer with a bare array, so the parser accepts every shape it reasonably
/// can; GuideJson does the finding and the small repairs. An unusable reply is an error, never a
/// guess: showing the user a made-up step is worse than saying "try again". "done" and "blocked"
/// may have no steps; "continue" must have some.
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
        var json = GuideJson.Payload(reply) ?? throw new FormatException("The reply had no JSON.");

        try
        {
            if (json.TrimStart().StartsWith('['))
            {
                var array = JsonSerializer.Deserialize<List<Flat>>(json, Options) ?? new List<Flat>();
                return MakePlan(null, null, array);
            }
            var envelope = JsonSerializer.Deserialize<Envelope>(json, Options)
                ?? throw new FormatException("The reply was not a valid plan.");
            // A single step object, which is what a model sends when it forgets the envelope. Worth
            // accepting: the step itself is still grounded against the live screen before it is used.
            if (envelope.Status is null && envelope.Steps is null)
            {
                var single = JsonSerializer.Deserialize<Flat>(json, Options);
                if (single?.Label is { Length: > 0 } || single?.Title is { Length: > 0 })
                    return MakePlan(null, null, new List<Flat> { single });
            }
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
        "done" or "finished" or "complete" or "completed" => GuidePlanStatus.Done,
        "blocked" or "stuck" => GuidePlanStatus.Blocked,
        _ => GuidePlanStatus.Proceed,
    };

    /// <summary>
    /// A step with no title but a usable label still points somewhere, so the label becomes the
    /// title rather than the whole reply being thrown away.
    /// </summary>
    private static GuideStep? MakeStep(Flat flat)
    {
        var title = flat.Title?.Trim();
        if (string.IsNullOrEmpty(title)) title = flat.Label?.Trim();
        if (string.IsNullOrEmpty(title)) return null;
        var action = Enum.TryParse<GuideAction>(flat.Action ?? "click", ignoreCase: true, out var parsed) ? parsed : GuideAction.Click;
        var label = flat.Label?.Trim();
        var target = string.IsNullOrEmpty(label) ? null : new GuideTarget(label, flat.Role, flat.Area);
        var typed = flat.Text?.Trim();
        return new GuideStep(title, flat.Detail, action, target, string.IsNullOrEmpty(typed) ? null : typed);
    }

    /// <summary>Kept so older call sites and checks keep working; GuideJson does the work now.</summary>
    public static string? ExtractJson(string text) => GuideJson.FirstValue(GuideJson.Unfenced(text));
}
