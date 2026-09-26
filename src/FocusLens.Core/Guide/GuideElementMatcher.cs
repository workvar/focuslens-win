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
    /// True when this step can still be done on the screen in front of the user. A step with no target
    /// stays. A named target must still be visible, and a different text field does not count.
    /// </summary>
    public static bool StillOnScreen(GuideStep step, IReadOnlyList<GuideElement> screen)
    {
        if (step.Target is not { } target) return true;
        return Best(target, screen, allowFieldFallback: false) is not null;
    }

    public static GuideElement? Best(GuideTarget target, IEnumerable<GuideElement> elements, bool allowFieldFallback = true)
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
    /// A field is often named only by a placeholder that UI Automation does not report. When the target is a
    /// field and no text matched, use the field nearest the top of the screen, where address and search bars
    /// live. Better a plausible field than a guide that stalls.
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

        int score;
        if (found == wanted) score = 100;
        else if (found.StartsWith(wanted, StringComparison.Ordinal) || wanted.StartsWith(found, StringComparison.Ordinal)) score = 75;
        else if (found.Contains(wanted, StringComparison.Ordinal)) score = 55;
        else return 0;

        if (target.Role is { } role && RoleGroups.TryGetValue(role, out var group))
            score += group.Contains(element.Role, StringComparer.OrdinalIgnoreCase) ? 15 : -10;

        if (!string.IsNullOrWhiteSpace(target.Area) &&
            target.Area.Contains(element.AppName, StringComparison.OrdinalIgnoreCase))
            score += 5;

        return score;
    }

    private static string Normalise(string text) => text.Trim().ToLowerInvariant();
}
