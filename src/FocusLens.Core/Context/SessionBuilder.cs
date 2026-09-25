namespace FocusLens.Core.Context;

/// <summary>
/// Groups consecutive screen-text snapshots of the same window into sessions and keeps
/// only the lines that newly appeared during each one.
/// </summary>
public static class SessionBuilder
{
    public static readonly TimeSpan MaxGap = TimeSpan.FromMinutes(5);
    public const int MaxLinesPerSession = 80;
    public const int MinLineLength = 12;

    public static string Key(string appId, string? title) => appId + "|" + (title ?? "");

    public static IReadOnlyList<SessionNote> Build(
        IEnumerable<ScreenTextSnapshot> snapshots, IReadOnlyDictionary<string, string> urls)
    {
        var notes = new List<SessionNote>();
        Accumulator? current = null;

        foreach (var snapshot in snapshots)
        {
            var key = Key(snapshot.AppId, snapshot.Title);
            var sessionKey = key + (snapshot.IsBackground ? "|bg" : "|fg");

            if (current is not null && current.SessionKey == sessionKey && snapshot.Timestamp - current.End <= MaxGap)
            {
                current.Add(snapshot);
            }
            else
            {
                if (current is not null) notes.Add(current.ToNote(urls));
                current = new Accumulator(key, sessionKey, snapshot);
                current.Add(snapshot);
            }
        }
        if (current is not null) notes.Add(current.ToNote(urls));
        return notes.Where(n => n.NewLines.Count > 0).ToList();
    }

    private sealed class Accumulator
    {
        private readonly HashSet<string> _seen = new();
        private readonly List<string> _lines = new();
        private readonly string _key;
        private readonly ScreenTextSnapshot _first;
        private int _count;

        public string SessionKey { get; }
        public DateTime End { get; private set; }

        public Accumulator(string key, string sessionKey, ScreenTextSnapshot first)
        {
            _key = key;
            SessionKey = sessionKey;
            _first = first;
            End = first.Timestamp;
        }

        public void Add(ScreenTextSnapshot snapshot)
        {
            End = snapshot.Timestamp;
            _count++;
            foreach (var raw in snapshot.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var line = raw.Trim();
                if (line.Length < MinLineLength || !line.Any(char.IsLetter) || _lines.Count >= MaxLinesPerSession)
                    continue;
                if (_seen.Add(line.ToLowerInvariant())) _lines.Add(line);
            }
        }

        public SessionNote ToNote(IReadOnlyDictionary<string, string> urls) => new(
            _first.AppName, _first.Title, urls.GetValueOrDefault(_key),
            _first.Timestamp, End, _count, _lines.ToList(), _first.IsBackground);
    }
}
