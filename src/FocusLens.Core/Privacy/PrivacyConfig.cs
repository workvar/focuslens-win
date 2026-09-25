using FocusLens.Core.Paths;
using FocusLens.Core.Storage;

namespace FocusLens.Core.Privacy;

/// <summary>Single source of truth for sensitive-category exclusions, persisted as privacy.json.</summary>
public sealed class PrivacyConfig
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public List<PrivacyCategory> Categories { get; set; } = new();

    public static PrivacyConfig Load() =>
        JsonFile.Load(AppPaths.PrivacyFile, PrivacyDefaults.Create);

    public bool Save() => JsonFile.Save(AppPaths.PrivacyFile, this);

    /// <summary>Name of the first enabled category that restricts this context, or null if allowed.</summary>
    public string? RestrictingCategory(string appId, string? url, string? windowTitle)
    {
        var host = HostOf(url);
        var haystacks = new List<string>();
        if (!string.IsNullOrEmpty(url)) haystacks.Add(url);
        if (!string.IsNullOrEmpty(windowTitle)) haystacks.Add(windowTitle);

        foreach (var category in Categories.Where(c => c.IsEnabled))
            if (category.Matches(appId, host, haystacks)) return category.Name;
        return null;
    }

    /// <summary>True if OCR'd screen text contains any enabled category's keyword.</summary>
    public bool OcrIsSensitive(string text)
    {
        var lower = text.ToLowerInvariant();
        return Categories.Where(c => c.IsEnabled).Any(c => c.KeywordHit(lower));
    }

    public static string? HostOf(string? urlString)
    {
        if (string.IsNullOrEmpty(urlString)) return null;
        if (!Uri.TryCreate(urlString, UriKind.Absolute, out var uri)) return null;
        var host = uri.Host;
        return host.StartsWith("www.") ? host[4..] : host;
    }
}
