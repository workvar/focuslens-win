namespace FocusLens.Core.Models;

public sealed class Category
{
    public long? Id { get; set; }
    public string Name { get; set; } = "";
    public string ColorHex { get; set; } = "#9CA3AF";
    public bool IsSystem { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum RuleMatchType
{
    BundleId,
    UrlDomain,
}

public static class RuleMatchTypeExtensions
{
    public static string ToDb(this RuleMatchType type) =>
        type == RuleMatchType.BundleId ? "bundle_id" : "url_domain";

    public static RuleMatchType FromDb(string value) =>
        value == "url_domain" ? RuleMatchType.UrlDomain : RuleMatchType.BundleId;
}

/// <summary>Maps an app (exe name) or URL domain to a category, or excludes it.</summary>
public sealed class AppRule
{
    public long? Id { get; set; }
    public string MatchType { get; set; } = "bundle_id";
    public string MatchValue { get; set; } = "";
    public long? CategoryId { get; set; }
    public bool IsExcluded { get; set; }
    public bool UserOverride { get; set; }
    public DateTime CreatedAt { get; set; }
}
