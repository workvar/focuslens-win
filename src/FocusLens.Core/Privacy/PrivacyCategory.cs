namespace FocusLens.Core.Privacy;

/// <summary>
/// A sensitive-content category. When enabled, matching activity is not recorded.
/// AppIds are executable names such as "1password.exe" (case-insensitive).
/// </summary>
public sealed class PrivacyCategory
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string ColorHex { get; set; } = "#9CA3AF";
    public bool IsSystem { get; set; }
    public bool IsEnabled { get; set; }
    public List<string> AppIds { get; set; } = new();
    /// <summary>Suffix-matched; a leading "*." is tolerated.</summary>
    public List<string> Domains { get; set; } = new();
    /// <summary>Case-insensitive substring match.</summary>
    public List<string> Keywords { get; set; } = new();

    public bool Matches(string appId, string? host, IEnumerable<string> haystacks)
    {
        if (AppIds.Any(a => string.Equals(a, appId, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (host is not null)
        {
            var lowerHost = host.ToLowerInvariant();
            foreach (var raw in Domains)
            {
                var domain = raw.ToLowerInvariant();
                if (domain.StartsWith("*.")) domain = domain[2..];
                if (domain.Length == 0) continue;
                if (lowerHost == domain || lowerHost.EndsWith("." + domain)) return true;
            }
        }

        if (Keywords.Count > 0)
        {
            foreach (var haystack in haystacks)
                if (KeywordHit(haystack.ToLowerInvariant())) return true;
        }
        return false;
    }

    public bool KeywordHit(string lowercasedText) =>
        Keywords.Any(k => k.Length > 0 && lowercasedText.Contains(k.ToLowerInvariant()));
}
