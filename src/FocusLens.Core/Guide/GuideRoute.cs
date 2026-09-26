using System.Text.Json;
using System.Text.Json.Serialization;

namespace FocusLens.Core.Guide;

/// <summary>
/// The route: a coarse plan of the whole task, made once before the first step.
///
/// Guide plans one to three actions at a time against the live screen, which is what keeps a plan
/// from going stale. The cost was that every replan re-derived the point of the task from scratch,
/// so the model would wander: open the downloads page, go back, open it again. A route fixes the
/// destination once and is then quoted in every later prompt, so each replan is a question about
/// the next move rather than about what the user wanted.
///
/// It names no controls and no coordinates, only milestones in plain words, so it cannot go stale
/// the way a step list does.
/// </summary>
public sealed record GuideRoute(string Goal, string? App, IReadOnlyList<string> Milestones, bool NeedsBrowser)
{
    public const int MaxMilestones = 6;

    public bool IsUsable => Goal.Length > 0 || Milestones.Count > 0;
}

public static class GuideRouteParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private sealed class Envelope
    {
        public string? Goal { get; set; }
        public string? App { get; set; }
        public bool? Browser { get; set; }
        public List<string>? Milestones { get; set; }
    }

    /// <summary>Null when the reply was not a route. The caller treats that as "no route", never as a failure.</summary>
    public static GuideRoute? Parse(string reply)
    {
        var json = GuideJson.Payload(reply);
        if (json is null) return null;
        Envelope? envelope;
        try { envelope = JsonSerializer.Deserialize<Envelope>(json, Options); }
        catch (JsonException) { return null; }
        if (envelope is null) return null;

        var milestones = (envelope.Milestones ?? new List<string>())
            .Select(m => m.Trim())
            .Where(m => m.Length > 0)
            .Take(GuideRoute.MaxMilestones)
            .ToList();
        var app = envelope.App?.Trim();
        return new GuideRoute(
            envelope.Goal?.Trim() ?? string.Empty,
            string.IsNullOrEmpty(app) ? null : app,
            milestones,
            envelope.Browser ?? false);
    }
}
