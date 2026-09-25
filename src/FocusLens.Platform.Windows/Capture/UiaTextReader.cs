using System.Text;
using System.Windows.Automation;
using FocusLens.Platform.Windows.Capture.Uia;

namespace FocusLens.Platform.Windows.Capture;

/// <summary>Reads visible text from a window through UI Automation (no screenshots).</summary>
public static class UiaTextReader
{
    private const int MaxElements = 300;
    private const int MaxChars = 8000;
    private const int MaxListItems = 150;

    public static string Read(IntPtr hwnd)
    {
        AccessibilityNudge.Apply(hwnd);
        var root = UiaSearch.FromHandle(hwnd);
        if (root is null) return "";

        var text = new StringBuilder();
        var seen = new HashSet<string>();

        void Add(string? line)
        {
            if (string.IsNullOrWhiteSpace(line) || text.Length >= MaxChars) return;
            var trimmed = line.Trim();
            if (trimmed.Length < 3 || !seen.Add(trimmed)) return;
            text.AppendLine(trimmed);
        }

        foreach (var document in UiaSearch.FindAll(root, ControlType.Document, 4))
            Add(UiaSearch.DocumentText(document, MaxChars));

        foreach (var element in UiaSearch.FindAll(root, ControlType.Text, MaxElements))
            Add(UiaSearch.Name(element));

        // Chat apps (WhatsApp, Teams, Slack) expose each message as a list item named with its text.
        foreach (var item in UiaSearch.FindAll(root, ControlType.ListItem, MaxListItems))
        {
            var name = UiaSearch.Name(item);
            Add(name.Length > 400 ? name[..400] : name);
        }

        foreach (var edit in UiaSearch.FindAll(root, ControlType.Edit, 20))
        {
            var value = UiaSearch.Value(edit);
            if (value is { Length: > 0 and < 500 }) Add(value);
        }

        return text.Length > MaxChars ? text.ToString(0, MaxChars) : text.ToString();
    }
}
