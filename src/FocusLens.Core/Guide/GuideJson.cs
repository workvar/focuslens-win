namespace FocusLens.Core.Guide;

/// <summary>
/// Getting JSON out of a model's reply. Shared by the plan parser and the route parser, because
/// both fail the same ways.
///
/// Models wrap JSON in ```json fences, put prose before and after it, leave a trailing comma, or
/// write their reasoning first. The old "first brace to last brace" slice broke whenever prose
/// after the JSON contained a brace, which is exactly what a chatty model does when it explains
/// its plan afterwards. This scans braces properly instead, skipping string contents so a "}"
/// inside a label cannot close the object early.
/// </summary>
public static class GuideJson
{
    private static readonly (string Open, string Close)[] ReasoningTags =
    {
        ("<think>", "</think>"), ("<thinking>", "</thinking>"), ("<reasoning>", "</reasoning>"),
    };

    /// <summary>
    /// Reasoning models write their thinking first, and that text contains braces that would be
    /// mistaken for the start of the answer.
    /// </summary>
    public static string StripReasoning(string text)
    {
        foreach (var (open, close) in ReasoningTags)
        {
            int start;
            while ((start = text.IndexOf(open, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                var end = text.IndexOf(close, start, StringComparison.OrdinalIgnoreCase);
                if (end < 0) { text = text[..start]; break; }
                text = text.Remove(start, end + close.Length - start);
            }
        }
        return text;
    }

    /// <summary>Everything inside a ``` fence, when the reply used one.</summary>
    public static string Unfenced(string text)
    {
        var open = text.IndexOf("```", StringComparison.Ordinal);
        if (open < 0) return text;
        var body = text[(open + 3)..];
        var newline = body.IndexOf('\n');
        if (newline >= 0)
        {
            var tag = body[..newline].Trim();
            if (tag.Length <= 8 && !tag.Contains('{') && !tag.Contains('[')) body = body[(newline + 1)..];
        }
        var close = body.IndexOf("```", StringComparison.Ordinal);
        return close < 0 ? body : body[..close];
    }

    /// <summary>
    /// The first complete JSON object or array, found by counting braces while skipping string
    /// contents and escapes. A reply cut off mid-object comes back unbalanced, for Repaired to close.
    /// </summary>
    public static string? FirstValue(string text)
    {
        var start = text.IndexOfAny(new[] { '{', '[' });
        if (start < 0) return null;
        var opener = text[start];
        var closer = opener == '{' ? '}' : ']';
        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (escaped) { escaped = false; continue; }
            if (c == '\\' && inString) { escaped = true; continue; }
            if (c == '"') { inString = !inString; continue; }
            if (inString) continue;
            if (c == opener) depth++;
            else if (c == closer && --depth == 0) return text[start..(i + 1)];
        }
        return text[start..];
    }

    /// <summary>
    /// Small syntax the model gets wrong: smart quotes from a copy-paste, and an object left
    /// unclosed by a reply that hit its token limit. Trailing commas are handled by the reader.
    /// </summary>
    public static string Repaired(string json)
    {
        var text = json
            .Replace('“', '"').Replace('”', '"')
            .Replace('‘', '\'').Replace('’', '\'');
        return Closed(text);
    }

    /// <summary>
    /// Closes what the model never finished, so a reply cut off by a token limit still yields the
    /// steps it did manage to write.
    /// </summary>
    private static string Closed(string text)
    {
        var stack = new Stack<char>();
        var inString = false;
        var escaped = false;
        foreach (var c in text)
        {
            if (escaped) { escaped = false; continue; }
            if (c == '\\' && inString) { escaped = true; continue; }
            if (c == '"') { inString = !inString; continue; }
            if (inString) continue;
            if (c is '{' or '[') stack.Push(c);
            else if (c is '}' or ']' && stack.Count > 0) stack.Pop();
        }
        if (!inString && stack.Count == 0) return text;

        var builder = new System.Text.StringBuilder(text);
        if (inString) builder.Append('"');
        // A cut-off value leaves a dangling key or comma; trimming back to the last complete entry
        // is more reliable than guessing the missing value.
        while (builder.Length > 0 && (builder[^1] == ',' || builder[^1] == ':' || char.IsWhiteSpace(builder[^1])))
            builder.Length--;
        foreach (var opener in stack) builder.Append(opener == '{' ? '}' : ']');
        return builder.ToString();
    }

    /// <summary>Reasoning stripped, fences removed, first balanced value taken, syntax repaired.</summary>
    public static string? Payload(string reply)
    {
        var value = FirstValue(Unfenced(StripReasoning(reply)));
        return value is null ? null : Repaired(value);
    }
}
