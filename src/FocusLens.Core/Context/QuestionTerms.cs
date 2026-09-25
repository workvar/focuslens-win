namespace FocusLens.Core.Context;

/// <summary>Turns a chat question into the few words worth searching for.</summary>
public static class QuestionTerms
{
    private static readonly HashSet<string> Stopwords = new()
    {
        "what", "when", "where", "which", "who", "why", "did", "does", "the", "and", "for", "was", "were", "with",
        "about", "that", "this", "from", "have", "how", "you", "your", "read", "see", "saw", "link", "page", "site",
        "know", "anything", "tell", "show", "find", "talked", "talk", "conversed", "chat", "chatted", "friend",
        "doing", "done", "computer", "laptop", "something", "some", "can", "could", "would", "hey", "please", "any",
        "also", "just", "then", "there", "them", "they", "him", "her", "his", "she", "mine", "been", "being", "had",
        "has", "are", "not", "use", "used", "using", "open", "opened", "last", "week", "month", "yesterday", "today",
        "ago", "time", "earlier", "recent", "recently", "focused", "focus", "score", "spent", "much", "long", "many",
        "our", "out", "into", "over", "let", "know", "does", "still", "again", "same", "thing", "things",
    };

    public static List<string> Keywords(string question) =>
        TextTokens.Words(question)
            .Where(t => !Stopwords.Contains(t))
            .Select(Stem)
            .Distinct()
            .ToList();

    /// <summary>Drops a plural "s" so "assignments" also finds "assignment".</summary>
    private static string Stem(string word) =>
        word.Length > 4 && word.EndsWith('s') && !word.EndsWith("ss") ? word[..^1] : word;
}
