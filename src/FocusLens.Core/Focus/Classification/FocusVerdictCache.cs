using System.Text.RegularExpressions;

namespace FocusLens.Core.Focus.Classification;

/// <summary>
/// Remembers what the model said, so the same page is judged once.
///
///   ON or OFF   kept for the rest of the session
///   unknown     kept for <see cref="UnknownRetry"/>, so a slow, cold or unreachable model is
///               not asked again every two seconds
///
/// Keys ignore the parts of a title that change while the page stays the same, such as "(3) "
/// unread counts, so a new message does not trigger a new call.
/// </summary>
public sealed partial class FocusVerdictCache
{
    public static readonly TimeSpan UnknownRetry = TimeSpan.FromSeconds(30);
    public const int Capacity = 300;

    private readonly Dictionary<string, FocusVerdict> _definite = new();
    /// <summary>Insertion order of the definite answers, oldest first, for eviction.</summary>
    private readonly Queue<string> _order = new();
    private readonly Dictionary<string, DateTime> _unknownUntil = new();

    public FocusVerdict? Lookup(string key, DateTime now)
    {
        if (_definite.TryGetValue(key, out var verdict)) return verdict;
        if (_unknownUntil.TryGetValue(key, out var until) && now < until) return FocusVerdict.Unknown;
        return null;
    }

    public void Store(FocusVerdict verdict, string key, DateTime now)
    {
        if (verdict == FocusVerdict.Unknown)
        {
            _unknownUntil[key] = now + UnknownRetry;
            if (_unknownUntil.Count > Capacity)
                foreach (var stale in _unknownUntil.Where(p => p.Value <= now).Select(p => p.Key).ToList())
                    _unknownUntil.Remove(stale);
            return;
        }

        _unknownUntil.Remove(key);
        if (!_definite.ContainsKey(key))
        {
            _order.Enqueue(key);
            if (_order.Count > Capacity) _definite.Remove(_order.Dequeue());
        }
        _definite[key] = verdict;
    }

    public void Clear()
    {
        _definite.Clear();
        _order.Clear();
        _unknownUntil.Clear();
    }

    public static string Key(string goal, FocusContext context) =>
        string.Join("|", goal.ToLowerInvariant(), context.Key, NormalizedTitle(context.WindowTitle));

    /// <summary>Lowercased title without leading unread counts ("(3) ", "(99+) ") or bullets.</summary>
    public static string NormalizedTitle(string title)
    {
        var text = title.Trim();
        while (LeadingNoise().Match(text) is { Success: true } match) text = text[match.Length..];
        return text.ToLowerInvariant();
    }

    [GeneratedRegex(@"^(\(\d+\+?\)|[•●*])\s*")]
    private static partial Regex LeadingNoise();
}
