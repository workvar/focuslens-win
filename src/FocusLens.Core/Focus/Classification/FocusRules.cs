namespace FocusLens.Core.Focus.Classification;

/// <summary>Rule-based checks that run before the model: things that are never a distraction, whatever the goal.</summary>
public static class FocusRules
{
    /// <summary>
    /// True when the app name, the executable (with or without ".exe") or the site is on the
    /// allowlist. A site entry also covers its subdomains.
    /// </summary>
    public static bool IsAllowed(FocusContext context, IReadOnlyList<string> allowlist) => allowlist.Any(entry =>
    {
        var appId = context.AppId.ToLowerInvariant();
        if (appId == entry || Path.GetFileNameWithoutExtension(appId) == entry) return true;
        if (context.AppName.ToLowerInvariant() == entry) return true;
        return context.Host is { } host && (host == entry || host.EndsWith("." + entry));
    });
}
