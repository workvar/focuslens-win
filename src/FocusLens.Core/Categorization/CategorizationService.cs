using FocusLens.Core.Data;
using FocusLens.Core.Models;
using FocusLens.Core.Storage;

namespace FocusLens.Core.Categorization;

/// <summary>
/// Assigns a category to an activity. Priority: user override by app, user override
/// by URL domain, built-in app rule, built-in domain rule, then "Other".
/// Rules are cached in memory and reloaded every 30 seconds.
/// </summary>
public sealed class CategorizationService
{
    private sealed class Rule { public string Key { get; set; } = ""; public string Category { get; set; } = ""; }
    private sealed class DefaultsFile
    {
        public List<Rule> AppRules { get; set; } = new();
        public List<Rule> UrlDomainRules { get; set; } = new();
    }

    private sealed class Cache
    {
        public Dictionary<string, string> UserApp = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> UserDomain = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> DefaultApp = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> DefaultDomain = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, long> CategoryIds = new();
        public Dictionary<string, string> CategoryColors = new();
    }

    private readonly Db _db;
    private readonly object _gate = new();
    private Cache _cache = new();
    private DateTime _loadedAt = DateTime.MinValue;

    public CategorizationService(Db db) => _db = db;

    public string CategoryName(string appId, string? url)
    {
        var cache = Snapshot();
        var domain = PrivacyHost(url);

        if (cache.UserApp.TryGetValue(appId, out var c1)) return c1;
        if (domain is not null && cache.UserDomain.TryGetValue(domain, out var c2)) return c2;
        if (cache.DefaultApp.TryGetValue(appId, out var c3)) return c3;
        if (domain is not null && cache.DefaultDomain.TryGetValue(domain, out var c4)) return c4;
        return "Other";
    }

    public long? CategoryId(string appId, string? url) =>
        Snapshot().CategoryIds.TryGetValue(CategoryName(appId, url), out var id) ? id : null;

    public string ColorHex(string categoryName) =>
        Snapshot().CategoryColors.TryGetValue(categoryName, out var hex) ? hex : "#A8A29E";

    public void Invalidate()
    {
        lock (_gate) _loadedAt = DateTime.MinValue;
    }

    private Cache Snapshot()
    {
        lock (_gate)
        {
            if (DateTime.UtcNow - _loadedAt > TimeSpan.FromSeconds(30))
            {
                _cache = Load();
                _loadedAt = DateTime.UtcNow;
            }
            return _cache;
        }
    }

    private Cache Load()
    {
        var cache = new Cache();
        try
        {
            var categories = _db.Query<Category>("SELECT * FROM categories").ToList();
            foreach (var category in categories)
            {
                cache.CategoryIds[category.Name] = category.Id ?? 0;
                cache.CategoryColors[category.Name] = category.ColorHex;
            }

            var rules = _db.Query<AppRule>("SELECT * FROM app_rules WHERE user_override = 1");
            foreach (var rule in rules)
            {
                var name = categories.FirstOrDefault(c => c.Id == rule.CategoryId)?.Name;
                if (name is null) continue;
                if (RuleMatchTypeExtensions.FromDb(rule.MatchType) == RuleMatchType.BundleId)
                    cache.UserApp[rule.MatchValue] = name;
                else
                    cache.UserDomain[rule.MatchValue] = name;
            }
        }
        catch
        {
            // Keep whatever we could load; the built-in defaults below still apply.
        }

        var defaults = EmbeddedJson.Load<DefaultsFile>("DefaultCategories.json");
        if (defaults is not null)
        {
            foreach (var rule in defaults.AppRules) cache.DefaultApp[rule.Key] = rule.Category;
            foreach (var rule in defaults.UrlDomainRules) cache.DefaultDomain[rule.Key] = rule.Category;
        }
        return cache;
    }

    private static string? PrivacyHost(string? url) => Privacy.PrivacyConfig.HostOf(url);
}
