using System.Text;

namespace FocusLens.Core.Context;

/// <summary>Picks the sessions most relevant to a question and renders them within a character budget.</summary>
public static class SessionRetriever
{
    private static readonly HashSet<string> Stopwords = new()
    {
        "what", "when", "where", "which", "did", "does", "the", "and", "for",
        "was", "were", "with", "about", "that", "this", "from", "have", "how",
        "you", "your", "read", "see", "saw", "today", "link", "page", "site",
    };

    public static string Render(IReadOnlyList<SessionNote> notes, string question, int charBudget = 6000)
    {
        if (notes.Count == 0) return "";
        var terms = Keywords(question);

        var ranked = notes
            .Select((note, index) => (note, score: Score(note, terms), recency: index))
            .OrderByDescending(x => x.score).ThenByDescending(x => x.recency);

        var chosen = new List<SessionNote>();
        var used = 0;
        foreach (var item in ranked)
        {
            var block = Format(item.note, 1200);
            if (used + block.Length > charBudget) continue;
            chosen.Add(item.note);
            used += block.Length;
        }

        return string.Join("\n", chosen.OrderBy(n => n.Start).Select(n => Format(n, 1200)));
    }

    public static List<string> Keywords(string question) =>
        TextTokens.Words(question).Where(t => !Stopwords.Contains(t)).ToList();

    private static int Score(SessionNote note, List<string> terms)
    {
        var head = $"{note.AppName} {note.Title} {note.Url}".ToLowerInvariant();
        var body = string.Join(" ", note.NewLines).ToLowerInvariant();
        return terms.Sum(t => (head.Contains(t) ? 3 : 0) + (body.Contains(t) ? 1 : 0));
    }

    private static string Format(SessionNote note, int lineBudget)
    {
        var header = new StringBuilder(
            $"  [{note.Start.ToLocalTime():MMM d HH:mm} to {note.End.ToLocalTime():MMM d HH:mm}] {note.AppName}");
        if (note.IsBackground) header.Append(" (background window, not focused)");
        if (!string.IsNullOrEmpty(note.Title)) header.Append($", window \"{note.Title}\"");
        if (note.Url is not null) header.Append($", URL: {note.Url}");

        var lines = new List<string> { header.ToString() };
        var used = 0;
        foreach (var line in note.NewLines)
        {
            var clipped = line.Length > 160 ? line[..160] + "..." : line;
            if (used + clipped.Length > lineBudget) break;
            lines.Add("    - " + clipped);
            used += clipped.Length;
        }
        return string.Join("\n", lines);
    }
}
