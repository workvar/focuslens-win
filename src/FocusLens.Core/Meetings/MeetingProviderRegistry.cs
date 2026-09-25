using System.Text.RegularExpressions;
using FocusLens.Core.Storage;

namespace FocusLens.Core.Meetings;

public enum MatchConfidence
{
    Low,
    Medium,
    High,
}

public sealed record MeetingProviderMatch(MeetingProvider Provider, MatchConfidence Confidence, string? ConferencingUrl);

/// <summary>Recognises meeting providers from app names, URLs and window titles (data-driven).</summary>
public sealed class MeetingProviderRegistry
{
    public sealed class Entry
    {
        public string Id { get; set; } = "";
        public List<string> AppIds { get; set; } = new();
        public List<string> UrlPatterns { get; set; } = new();
        public List<string> TitlePatterns { get; set; } = new();
        public MatchConfidence Confidence { get; set; } = MatchConfidence.Low;
    }

    private sealed class FileModel
    {
        public int Version { get; set; }
        public List<Entry> Providers { get; set; } = new();
    }

    public IReadOnlyList<Entry> Entries { get; }

    public MeetingProviderRegistry(IEnumerable<Entry> entries) => Entries = entries.ToList();

    public static MeetingProviderRegistry LoadBundled() =>
        new(EmbeddedJson.Load<FileModel>("MeetingProviders.json")?.Providers ?? new List<Entry>());

    public MeetingProviderMatch? Match(string appId)
    {
        var entry = Entries.FirstOrDefault(e => e.AppIds.Any(a => a.Equals(appId, StringComparison.OrdinalIgnoreCase)));
        return entry is null ? null : new(MeetingProviderExtensions.FromId(entry.Id), entry.Confidence, null);
    }

    public MeetingProviderMatch? MatchUrls(IEnumerable<string> urls)
    {
        foreach (var url in urls)
        {
            var normalised = Normalise(url);
            foreach (var entry in Entries)
                if (entry.UrlPatterns.Any(p => WildcardMatch(normalised, p)))
                    return new(MeetingProviderExtensions.FromId(entry.Id), entry.Confidence, url);
        }
        return null;
    }

    public MeetingProviderMatch? MatchTitles(IEnumerable<string> titles)
    {
        foreach (var title in titles)
            foreach (var entry in Entries)
                if (entry.TitlePatterns.Any(p => Regex.IsMatch(title, p, RegexOptions.IgnoreCase)))
                    return new(MeetingProviderExtensions.FromId(entry.Id),
                        (MatchConfidence)Math.Min((int)entry.Confidence, (int)MatchConfidence.Medium), null);
        return null;
    }

    /// <summary>Lowercases and strips scheme, "www.", query, fragment and trailing slashes.</summary>
    public static string Normalise(string url)
    {
        var text = url.ToLowerInvariant();
        foreach (var prefix in new[] { "https://", "http://" })
            if (text.StartsWith(prefix)) { text = text[prefix.Length..]; break; }
        if (text.StartsWith("www.")) text = text[4..];
        var cut = text.IndexOfAny(new[] { '?', '#' });
        if (cut >= 0) text = text[..cut];
        return text.TrimEnd('/');
    }

    /// <summary>Glob match where "*" matches any run of characters.</summary>
    public static bool WildcardMatch(string text, string pattern)
    {
        var parts = pattern.ToLowerInvariant().Split('*');
        if (parts.Length == 1) return text == pattern.ToLowerInvariant();

        var index = 0;
        if (parts[0].Length > 0)
        {
            if (!text.StartsWith(parts[0], StringComparison.Ordinal)) return false;
            index = parts[0].Length;
        }
        var last = parts[^1];
        if (last.Length > 0 && !text.EndsWith(last, StringComparison.Ordinal)) return false;

        for (var i = 1; i < parts.Length - 1; i++)
        {
            if (parts[i].Length == 0) continue;
            var found = text.IndexOf(parts[i], index, StringComparison.Ordinal);
            if (found < 0) return false;
            index = found + parts[i].Length;
        }
        return true;
    }
}
