namespace FocusLens.Core.Focus.Classification;

/// <summary>The question sent to the model and the parser for its answer. Kept apart from the network code.</summary>
public static class FocusPrompt
{
    public static string Build(string goal, FocusContext context)
    {
        var lines = new List<string>
        {
            "You judge whether what a person is looking at helps their current goal.",
            $"Goal: {goal}",
            $"App: {context.AppName}",
            $"Window title: {(context.WindowTitle.Length == 0 ? "(none)" : context.WindowTitle)}",
        };
        if (context.Url is { } url) lines.Add($"URL: {url}");
        lines.AddRange(new[]
        {
            "",
            "Answer ON if it plausibly helps the goal. That includes documentation,",
            "search results, reference material, and tools used to do the work.",
            "Answer OFF if it is entertainment, social media, shopping, news, or",
            "otherwise unrelated to the goal. If you are unsure, answer ON.",
            "Reply with exactly one word: ON or OFF.",
        });
        return string.Join("\n", lines);
    }

    /// <summary>Reads the first ON or OFF word, ignoring any reasoning block.</summary>
    public static FocusVerdict Parse(string raw)
    {
        var text = raw;
        var open = text.IndexOf("<think>", StringComparison.Ordinal);
        if (open >= 0)
        {
            var close = text.IndexOf("</think>", open + 7, StringComparison.Ordinal);
            if (close < 0) return FocusVerdict.Unknown;
            text = text.Remove(open, close + 8 - open);
        }

        var first = text.ToUpperInvariant()
            .Split(c => !char.IsLetter(c))
            .FirstOrDefault(w => w.Length > 0);
        return first switch
        {
            "OFF" => FocusVerdict.OffTopic,
            "ON" => FocusVerdict.OnTopic,
            _ => FocusVerdict.Unknown,
        };
    }

    private static string[] Split(this string text, Func<char, bool> isSeparator)
    {
        var words = new List<string>();
        var start = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && !isSeparator(text[i])) continue;
            if (i > start) words.Add(text[start..i]);
            start = i + 1;
        }
        return words.ToArray();
    }
}
