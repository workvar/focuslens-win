using System.Text.RegularExpressions;

namespace FocusLens.Core.Context;

/// <summary>Splits text into lowercase alphanumeric words for keyword matching.</summary>
public static class TextTokens
{
    private static readonly Regex NonWord = new(@"[^\p{L}\p{N}]+", RegexOptions.Compiled);

    public static List<string> Words(string text, int minLength = 3) =>
        NonWord.Split(text.ToLowerInvariant())
            .Where(t => t.Length >= minLength).ToList();
}
