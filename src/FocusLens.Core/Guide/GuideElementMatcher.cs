namespace FocusLens.Core.Guide;

/// <summary>
/// Picks the on-screen element a step means. Text match decides first, the role the model
/// expected breaks ties, and the smaller frame wins last because it is the more specific thing
/// to point at. Pure: takes a list, returns one.
/// </summary>
public static class GuideElementMatcher
{
    /// <summary>Below this a match is a guess, and Guide would rather say it cannot find it.</summary>
    public const int Threshold = 50;

    private static readonly Dictionary<string, string[]> RoleGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        ["button"] = new[] { "Button", "SplitButton" },
        ["checkbox"] = new[] { "CheckBox" },
        ["switch"] = new[] { "CheckBox", "Button" },
        ["menu"] = new[] { "Menu", "MenuItem" },
        ["menuitem"] = new[] { "MenuItem" },
        ["tab"] = new[] { "TabItem" },
        ["row"] = new[] { "ListItem", "TreeItem", "DataItem", "Text" },
        ["field"] = new[] { "Edit", "ComboBox", "Document" },
        ["link"] = new[] { "Hyperlink" },
    };

    /// <summary>
    /// True when this step's label is still an exact control on screen. A step with no label is not
    /// kept: that is how an invented "look" step stays forever.
    /// </summary>
    public static bool StillOnScreen(GuideStep step, IReadOnlyList<GuideElement> screen)
    {
        if (step.Target is not { } target) return false;
        return Best(target, screen, allowFieldFallback: false) is not null;
    }

    /// <summary>
    /// The leading steps whose labels are copied from this screen, in order. Stops at the first label
    /// that is not listed, and keeps each control once. The label is rewritten to the element's own
    /// text so a later lookup cannot drift onto a different control that merely contains the same word.
    /// </summary>
    public static IReadOnlyList<GuideStep> Grounded(IReadOnlyList<GuideStep> steps, IReadOnlyList<GuideElement> screen)
    {
        var kept = new List<GuideStep>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in steps)
        {
            if (step.Target is not { } target) break;
            var element = Exact(target, screen);
            if (element is null) break;
            var key = Normalise(element.Label);
            if (!seen.Add(key)) continue;
            kept.Add(step with { Target = target with { Label = element.Label } });
        }
        return kept;
    }

    /// <summary>Labels from a plan that could not be grounded, for the next prompt.</summary>
    public static IReadOnlyList<string> InventedLabels(IReadOnlyList<GuideStep> steps)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var labels = new List<string>();
        foreach (var step in steps)
        {
            var raw = step.Target?.Label.Trim() ?? "";
            var label = raw.Length == 0 ? step.Title : raw;
            var key = Normalise(label);
            if (key.Length == 0 || !seen.Add(key)) continue;
            labels.Add(label);
            if (labels.Count == 6) break;
        }
        return labels;
    }

    public static bool SameLabel(string a, string b)
    {
        return GuideLabel.Same(a, b);
    }

    /// <summary>
    /// Interactive controls only, so a clock or other static text cannot hide the fact that a click
    /// did nothing.
    /// </summary>
    public static string Fingerprint(IReadOnlyList<GuideElement> elements)
    {
        var keys = new List<string>();
        foreach (var element in elements)
        {
            if (string.Equals(element.Role, "Text", StringComparison.OrdinalIgnoreCase)) continue;
            var label = Normalise(element.Label);
            if (label.Length == 0) continue;
            keys.Add($"{element.AppName}|{element.Window}|{element.Role}|{label}|{element.State}");
        }
        keys.Sort(StringComparer.Ordinal);
        return string.Join("\n", keys);
    }

    public static GuideElement? Best(GuideTarget target, IEnumerable<GuideElement> elements, bool allowFieldFallback = false)
    {
        GuideElement? best = null;
        var bestScore = 0;
        foreach (var element in elements)
        {
            var score = Score(target, element);
            if (score < Threshold) continue;
            if (best is null || score > bestScore || (score == bestScore && element.Frame.Area < best.Frame.Area))
            {
                best = element;
                bestScore = score;
            }
        }
        return best ?? (allowFieldFallback ? FieldFallback(target, elements) : null);
    }

    /// <summary>
    /// Exact label only. A shared prefix or a contained word is a different control, and pointing at it
    /// is how an invented step gets a cursor.
    /// </summary>
    private static GuideElement? Exact(GuideTarget target, IEnumerable<GuideElement> elements)
    {
        var wanted = Normalise(target.Label);
        if (wanted.Length == 0) return null;
        GuideElement? best = null;
        var bestScore = int.MinValue;
        foreach (var element in elements)
        {
            if (Normalise(element.Label) != wanted) continue;
            var score = 100;
            if (target.Role is { } role && RoleGroups.TryGetValue(role, out var group))
                score += group.Contains(element.Role, StringComparer.OrdinalIgnoreCase) ? 15 : -10;
            if (best is null || score > bestScore || (score == bestScore && element.Frame.Area < best.Frame.Area))
            {
                best = element;
                bestScore = score;
            }
        }
        return best;
    }

    /// <summary>
    /// A field is often named only by a placeholder that UI Automation does not report. Used only when a
    /// caller explicitly allows it. Guide pointing does not: an unnamed field would be a guess.
    /// </summary>
    private static GuideElement? FieldFallback(GuideTarget target, IEnumerable<GuideElement> elements)
    {
        if (!string.Equals(target.Role, "field", StringComparison.OrdinalIgnoreCase)) return null;
        return elements.Where(e => RoleGroups["field"].Contains(e.Role, StringComparer.OrdinalIgnoreCase))
                       .OrderBy(e => e.Frame.Y).FirstOrDefault();
    }

    public static int Score(GuideTarget target, GuideElement element)
    {
        var wanted = Normalise(target.Label);
        var found = Normalise(element.Label);
        if (wanted.Length == 0 || found.Length == 0) return 0;

        if (found != wanted) return 0;
        var score = 100;

        if (target.Role is { } role && RoleGroups.TryGetValue(role, out var group))
            score += group.Contains(element.Role, StringComparer.OrdinalIgnoreCase) ? 15 : -10;

        if (!string.IsNullOrWhiteSpace(target.Area) &&
            target.Area.Contains(element.AppName, StringComparison.OrdinalIgnoreCase))
            score += 5;

        return score;
    }

    private static string Normalise(string text) => GuideLabel.Normalise(text);
}
