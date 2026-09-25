using System.Text.RegularExpressions;

namespace FocusLens.Core.Meetings;

/// <summary>Parses the model's markdown summary back into note blocks and action items.</summary>
public static class MeetingSummaryParser
{
    public sealed class Result
    {
        public Dictionary<NoteBlockType, string> Blocks { get; } = new();
        public List<(string? Owner, string Text)> ActionItems { get; } = new();
    }

    private static readonly Dictionary<string, NoteBlockType> HeadingMap = new()
    {
        ["summary"] = NoteBlockType.Summary,
        ["overview"] = NoteBlockType.Summary,
        ["decisions"] = NoteBlockType.Decisions,
        ["decisions made"] = NoteBlockType.Decisions,
        ["action items"] = NoteBlockType.ActionItems,
        ["actions"] = NoteBlockType.ActionItems,
        ["next steps"] = NoteBlockType.ActionItems,
        ["open questions"] = NoteBlockType.OpenQuestions,
        ["questions"] = NoteBlockType.OpenQuestions,
        ["unresolved"] = NoteBlockType.OpenQuestions,
    };

    public static Result Parse(string markdown)
    {
        var result = new Result();
        NoteBlockType? current = null;
        var buffer = new List<string>();

        void Flush()
        {
            if (current is null) return;
            var body = string.Join("\n", buffer).Trim();
            if (body.Length > 0) result.Blocks[current.Value] = body;
            buffer.Clear();
        }

        foreach (var line in markdown.Split('\n'))
        {
            var type = HeadingType(line);
            if (type is not null)
            {
                Flush();
                current = type;
            }
            else if (current is not null)
            {
                buffer.Add(line.TrimEnd('\r'));
            }
        }
        Flush();

        if (result.Blocks.TryGetValue(NoteBlockType.ActionItems, out var actions))
            result.ActionItems.AddRange(ParseActionItems(actions));
        return result;
    }

    private static NoteBlockType? HeadingType(string line)
    {
        var text = line.Trim();
        if (text.Length == 0) return null;
        text = text.Trim('#', '*', '_', ' ');
        if (text.EndsWith(':')) text = text[..^1];
        text = text.Trim().ToLowerInvariant();
        if (text.Length > 24) return null;
        return HeadingMap.TryGetValue(text, out var type) ? type : null;
    }

    public static List<(string? Owner, string Text)> ParseActionItems(string markdown)
    {
        var items = new List<(string?, string)>();
        foreach (var raw in markdown.Split('\n'))
        {
            var text = raw.Trim();
            if (text.Length == 0) continue;

            foreach (var prefix in new[] { "- [ ]", "- [x]", "* [ ]", "-", "*", "•" })
                if (text.StartsWith(prefix)) { text = text[prefix.Length..].Trim(); break; }
            text = Regex.Replace(text, @"^\d+[.)]\s+", "");
            if (text.Length == 0) continue;

            items.Add(SplitOwner(text));
        }
        return items;
    }

    private static (string? Owner, string Text) SplitOwner(string text)
    {
        foreach (var separator in new[] { ": ", " — ", " – ", " - " })
        {
            var index = text.IndexOf(separator, StringComparison.Ordinal);
            if (index < 0) continue;
            var owner = text[..index].Trim();
            var body = text[(index + separator.Length)..].Trim();
            if (owner.Length > 0 && body.Length > 0 && owner.Length <= 32 && !owner.Contains(" the "))
                return (owner, body);
        }

        if (text.EndsWith(')') && text.LastIndexOf('(') is var open and >= 0)
        {
            var owner = text[(open + 1)..^1].Trim();
            var body = text[..open].Trim();
            if (owner.Length > 0 && body.Length > 0 && owner.Length <= 32 && !owner.Contains(' '))
                return (owner, body);
        }
        return (null, text);
    }
}
