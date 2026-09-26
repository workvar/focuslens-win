namespace FocusLens.Core.HoldFill;

/// <summary>
/// Decides whether a text field is a search field, from the words an accessibility tree gives it.
/// Shared by the platform readers so Windows and the Mac agree, and pure so it can be tested.
/// </summary>
public static class SearchFieldRules
{
    private static readonly string[] Words = { "search", "find", "query", "look up", "lookup" };

    /// <summary>
    /// Fields that mention search but are not a place for a query: "Find and replace" boxes and
    /// anything that asks for a secret.
    /// </summary>
    private static readonly string[] NotSearch = { "replace", "password", "passcode", "pin code" };

    /// <param name="hints">Name, placeholder, automation id, class name, role description: anything the field says about itself.</param>
    public static bool IsSearchLike(params string?[] hints)
    {
        var text = string.Join(" ", hints.Where(h => !string.IsNullOrWhiteSpace(h))).ToLowerInvariant();
        if (text.Length == 0) return false;
        if (NotSearch.Any(text.Contains)) return false;
        return Words.Any(text.Contains);
    }

    /// <summary>The first non-empty hint, trimmed and kept short, to show and to send to the model.</summary>
    public static string LabelFrom(params string?[] hints)
    {
        var label = hints.FirstOrDefault(h => !string.IsNullOrWhiteSpace(h))?.Trim() ?? "Search";
        return label.Length > 60 ? label[..60] : label;
    }
}
