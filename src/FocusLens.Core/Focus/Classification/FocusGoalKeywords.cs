namespace FocusLens.Core.Focus.Classification;

/// <summary>
/// Cheap on-topic shortcut. If a distinctive word from the goal appears as a whole word in the
/// app name, window title or URL, the page is on topic and the model does not need to be asked.
/// Whole words only, so "net" (from ".NET") does not match "Netflix".
/// </summary>
public static class FocusGoalKeywords
{
    private static readonly HashSet<string> Filler = new()
    {
        "i", "want", "wanna", "to", "the", "and", "for", "my", "some", "about", "with",
        "need", "study", "studying", "learn", "learning", "work", "working", "focus",
        "focusing", "finish", "complete", "read", "reading", "write", "writing", "on",
    };

    /// <summary>Lowercased words, keeping "#" and "+" so "c#" and "c++" survive.</summary>
    public static List<string> Tokens(string text)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var ch in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch) || ch is '#' or '+') current.Append(ch);
            else Flush();
        }
        Flush();
        return tokens;

        void Flush()
        {
            if (current.Length == 0) return;
            tokens.Add(current.ToString());
            current.Clear();
        }
    }

    public static HashSet<string> Keywords(string goal) => Tokens(goal)
        .Where(w => !Filler.Contains(w) && (w.Length >= 3 || w.Contains('#') || w.Contains('+')))
        .ToHashSet();

    public static bool Matches(string goal, IEnumerable<string> fields)
    {
        var wanted = Keywords(goal);
        if (wanted.Count == 0) return false;
        return fields.SelectMany(Tokens).Any(wanted.Contains);
    }
}
