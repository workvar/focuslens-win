using System.Windows.Automation;
using FocusLens.Platform.Windows.Capture.Uia;

namespace FocusLens.Platform.Windows.Capture;

/// <summary>Reads the address bar of the focused browser window through UI Automation.</summary>
public sealed class BrowserUrlReader
{
    private static readonly string[] AddressHints =
    {
        "address and search bar", "search or enter address", "enter address", "address bar", "url",
    };

    // The address-bar element is expensive to find, so keep it per window until it stops working.
    private readonly Dictionary<IntPtr, AutomationElement> _cache = new();

    public string? ReadUrl(IntPtr hwnd)
    {
        if (_cache.TryGetValue(hwnd, out var cached))
        {
            var value = TryRead(cached);
            if (value is not null) return value;
            _cache.Remove(hwnd);
        }

        var root = UiaSearch.FromHandle(hwnd);
        if (root is null) return null;

        var edit = UiaSearch.FindFirst(root, ControlType.Edit, IsAddressBar);
        if (edit is null) return null;

        var url = TryRead(edit);
        if (url is not null) _cache[hwnd] = edit;
        if (_cache.Count > 32) _cache.Clear();
        return url;
    }

    private static bool IsAddressBar(AutomationElement element)
    {
        var name = UiaSearch.Name(element).ToLowerInvariant();
        return AddressHints.Any(name.Contains);
    }

    private static string? TryRead(AutomationElement element) => Normalize(UiaSearch.Value(element));

    /// <summary>Adds a scheme when the omnibox shows a bare host, and rejects search text.</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var text = raw.Trim();
        if (text.Contains(' ')) return null; // a search query, not a URL

        if (!text.Contains("://"))
        {
            if (!text.Contains('.') && !text.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)) return null;
            text = "https://" + text;
        }
        return Uri.TryCreate(text, UriKind.Absolute, out _) ? text : null;
    }
}
