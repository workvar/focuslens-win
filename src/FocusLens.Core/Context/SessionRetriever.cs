using System.Text;

namespace FocusLens.Core.Context;

/// <summary>Picks the sessions most relevant to a question and renders them within a character budget.</summary>
public static class SessionRetriever
{
    private const int LineBudget = 1200;
    private const int FillerBudget = 1500;

    public static string Render(IReadOnlyList<SessionNote> notes, string question, int charBudget = 6000)
    {
        if (notes.Count == 0) return "";
        var terms = QuestionTerms.Keywords(question);

        var ranked = notes
            .Select((note, index) => (note, score: Score(note, terms), recency: index))
            .OrderByDescending(x => x.score).ThenByDescending(x => x.recency);

        var chosen = new List<SessionNote>();
        var used = 0;
        var filler = 0;
        foreach (var item in ranked)
        {
            var block = Format(item.note, LineBudget, terms);
            var isFiller = terms.Count > 0 && item.score == 0;
            if (used + block.Length > charBudget) continue;
            if (isFiller && filler + block.Length > FillerBudget) continue;
            chosen.Add(item.note);
            used += block.Length;
            if (isFiller) filler += block.Length;
        }

        return string.Join("\n", chosen.OrderBy(n => n.Start).Select(n => Format(n, LineBudget, terms)));
    }

    /// <summary>How many sessions mention at least one search term.</summary>
    public static int MatchCount(IReadOnlyList<SessionNote> notes, string question)
    {
        var terms = QuestionTerms.Keywords(question);
        return terms.Count == 0 ? 0 : notes.Count(n => Score(n, terms) > 0);
    }

    private static int Score(SessionNote note, List<string> terms)
    {
        var head = $"{note.AppName} {note.Title} {note.Url}".ToLowerInvariant();
        var body = string.Join(" ", note.NewLines).ToLowerInvariant();
        return terms.Sum(t => (head.Contains(t) ? 3 : 0) + (body.Contains(t) ? 1 : 0));
    }

    private static string Format(SessionNote note, int lineBudget, List<string> terms)
    {
        var header = new StringBuilder(
            $"  [{note.Start.ToLocalTime():MMM d HH:mm} to {note.End.ToLocalTime():MMM d HH:mm}] {note.AppName}");
        if (note.IsBackground) header.Append(" (background window, not focused)");
        if (!string.IsNullOrEmpty(note.Title)) header.Append($", window \"{note.Title}\"");
        if (note.Url is not null) header.Append($", URL: {note.Url}");

        var lines = new List<string> { header.ToString() };
        var used = 0;
        foreach (var line in MatchesFirst(note.NewLines, terms))
        {
            var clipped = line.Length > 240 ? line[..240] + "..." : line;
            if (used + clipped.Length > lineBudget) break;
            lines.Add("    - " + clipped);
            used += clipped.Length;
        }
        return string.Join("\n", lines);
    }

    /// <summary>Lines that contain a search term come first so they survive the line budget.</summary>
    private static IEnumerable<string> MatchesFirst(IReadOnlyList<string> lines, List<string> terms) =>
        terms.Count == 0
            ? lines
            : lines.OrderBy(l => terms.Any(t => l.Contains(t, StringComparison.OrdinalIgnoreCase)) ? 0 : 1);
}
