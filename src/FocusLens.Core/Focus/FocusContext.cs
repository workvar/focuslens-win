namespace FocusLens.Core.Focus;

/// <summary>A window rectangle in screen pixels, used to place the block overlay.</summary>
public readonly record struct FocusRect(int X, int Y, int Width, int Height);

/// <summary>
/// What the user is looking at right now. Plain data: no UI, no I/O, so the policy and the
/// classifier can be reasoned about (and tested) without a running app.
/// </summary>
public sealed class FocusContext : IEquatable<FocusContext>
{
    public required string AppName { get; init; }
    /// <summary>Lowercase executable name, such as "chrome.exe".</summary>
    public required string AppId { get; init; }
    public required uint Pid { get; init; }
    /// <summary>The window handle, so the closer and the overlay act on this exact window.</summary>
    public required long Window { get; init; }
    public required string WindowTitle { get; init; }
    public string? Url { get; init; }
    public required bool IsBrowser { get; init; }
    /// <summary>Left out of equality, so moving a window is not "a different page".</summary>
    public FocusRect? Bounds { get; init; }

    /// <summary>Lowercased host without a leading "www.", when the URL is known.</summary>
    public string? Host
    {
        get
        {
            if (Url is null || !Uri.TryCreate(Url, UriKind.Absolute, out var uri) || uri.Host.Length == 0) return null;
            var host = uri.Host.ToLowerInvariant();
            return host.StartsWith("www.") ? host[4..] : host;
        }
    }

    /// <summary>Identity used to group distractions: the site for browsers, the app otherwise.</summary>
    public string Key => IsBrowser && Host is { } host ? "site:" + host : "app:" + AppId;

    /// <summary>Short name to show the user.</summary>
    public string Label => IsBrowser ? Host ?? AppName : AppName;

    public string Noun => IsBrowser ? "tab" : "window";

    /// <summary>A browser with neither a title nor a URL says nothing about the page, and the model would call it on topic.</summary>
    public bool IsJudgeable => !IsBrowser || WindowTitle.Length > 0 || Url is not null;

    /// <summary>Same site or app, whatever the title or window position. Titles and URLs wobble while you scroll.</summary>
    public bool IsSameTarget(FocusContext other) => Pid == other.Pid && Key == other.Key;

    /// <summary>Same site or app and the same page. Used right before closing, so a different tab is never closed.</summary>
    public bool IsSameTab(FocusContext other)
    {
        if (!IsSameTarget(other)) return false;
        if (WindowTitle == other.WindowTitle) return true;
        return Url is not null && other.Url is not null && Url == other.Url;
    }

    public FocusContext WithBounds(FocusRect? bounds) => new()
    {
        AppName = AppName, AppId = AppId, Pid = Pid, Window = Window,
        WindowTitle = WindowTitle, Url = Url, IsBrowser = IsBrowser, Bounds = bounds,
    };

    public bool Equals(FocusContext? other) =>
        other is not null && Pid == other.Pid && AppId == other.AppId && WindowTitle == other.WindowTitle && Url == other.Url;

    public override bool Equals(object? obj) => Equals(obj as FocusContext);
    public override int GetHashCode() => HashCode.Combine(Pid, AppId, WindowTitle, Url);
    public static bool operator ==(FocusContext? a, FocusContext? b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(FocusContext? a, FocusContext? b) => !(a == b);
}
