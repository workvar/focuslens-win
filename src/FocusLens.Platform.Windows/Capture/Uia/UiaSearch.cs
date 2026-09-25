using System.Windows.Automation;

namespace FocusLens.Platform.Windows.Capture.Uia;

/// <summary>Small, defensive helpers over Microsoft UI Automation. Every call can throw when a window closes.</summary>
internal static class UiaSearch
{
    public static AutomationElement? FromHandle(IntPtr hwnd)
    {
        try { return AutomationElement.FromHandle(hwnd); }
        catch { return null; }
    }

    public static AutomationElement? FindFirst(AutomationElement root, ControlType type, Func<AutomationElement, bool> predicate)
    {
        try
        {
            var candidates = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, type));
            foreach (AutomationElement element in candidates)
            {
                try { if (predicate(element)) return element; }
                catch { /* element vanished mid-scan */ }
            }
        }
        catch { /* tree unavailable */ }
        return null;
    }

    public static List<AutomationElement> FindAll(AutomationElement root, ControlType type, int limit)
    {
        var found = new List<AutomationElement>();
        try
        {
            var candidates = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, type));
            foreach (AutomationElement element in candidates)
            {
                found.Add(element);
                if (found.Count >= limit) break;
            }
        }
        catch { /* tree unavailable */ }
        return found;
    }

    public static string Name(AutomationElement element)
    {
        try { return element.Current.Name ?? ""; }
        catch { return ""; }
    }

    public static string? Value(AutomationElement element)
    {
        try
        {
            return element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)
                ? ((ValuePattern)pattern).Current.Value
                : null;
        }
        catch { return null; }
    }

    public static string? DocumentText(AutomationElement element, int maxChars)
    {
        try
        {
            if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var pattern)) return null;
            return ((TextPattern)pattern).DocumentRange.GetText(maxChars);
        }
        catch { return null; }
    }
}
