using System.Diagnostics;
using System.Windows.Automation;
using FocusLens.Core.Guide;

namespace FocusLens.Platform.Windows.Guide;

/// <summary>
/// Lists what is on screen in the app the user is working in, through UI Automation, off the UI
/// thread. Each walk has a node budget, so a huge window (a browser with a long page) yields a
/// slightly emptier snapshot, never a stall. Rectangles come back in screen pixels.
/// </summary>
public sealed class UiaWalker
{
    private const int MaxNodesPerRoot = 450;
    private const int MaxDepth = 14;

    private static readonly HashSet<string> Roles = new()
    {
        "Button", "SplitButton", "CheckBox", "RadioButton", "MenuItem", "Menu", "TabItem",
        "Edit", "ComboBox", "Hyperlink", "ListItem", "TreeItem", "DataItem", "Text", "Slider",
    };

    private readonly int _ownPid = Environment.ProcessId;

    /// <summary>
    /// The window the user was working in before Guide's own prompt took focus. Set when the
    /// shortcut is pressed; used whenever the foreground window is FocusLens itself.
    /// </summary>
    public IntPtr PreferredWindow { get; set; }

    /// <summary>A snapshot never takes longer than this; whatever was read by then is used.</summary>
    public static readonly TimeSpan SnapshotTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Snapshot of the working window and the taskbar (Start, tray, quick settings). Never throws and
    /// never waits longer than <see cref="SnapshotTimeout"/>: a UI Automation call can block on a busy
    /// app, and before this the guide sat on "Thinking..." forever.
    /// </summary>
    public async Task<IReadOnlyList<GuideElement>> SnapshotAsync()
    {
        var output = new List<GuideElement>();
        var walk = Task.Run(() =>
        {
            var roots = new List<IntPtr> { WorkingWindow(), GuideNative.FindWindowW("Shell_TrayWnd", null) };
            foreach (var hwnd in roots.Where(h => h != IntPtr.Zero).Distinct())
            {
                try { Walk(hwnd, output); }
                catch
                {
                    // The window closed mid-walk, or the app refused UI Automation. Skip it.
                }
            }
        });
        try { await walk.WaitAsync(SnapshotTimeout); }
        catch (TimeoutException) { }
        lock (output) return output.ToList();
    }

    private IntPtr WorkingWindow()
    {
        var foreground = GuideNative.GetForegroundWindow();
        GuideNative.GetWindowThreadProcessId(foreground, out var pid);
        return pid == _ownPid && PreferredWindow != IntPtr.Zero ? PreferredWindow : foreground;
    }

    /// <summary>
    /// Breadth first, so the window's own chrome (toolbar, tabs, menu bar) is read before anything deep.
    /// A depth-first walk used to spend the whole node budget inside a web page, so the controls the
    /// planner needed were never reached. A web page's contents (Document) are not descended into.
    /// </summary>
    private static void Walk(IntPtr hwnd, List<GuideElement> output)
    {
        var root = AutomationElement.FromHandle(hwnd);
        var appName = AppNameOf(hwnd);
        var window = WindowTitle(root);
        var walker = TreeWalker.ControlViewWalker;
        var frontier = new Queue<(AutomationElement Element, int Depth)>();
        frontier.Enqueue((root, 0));
        var budget = MaxNodesPerRoot;

        while (frontier.Count > 0 && budget-- > 0)
        {
            var (element, depth) = frontier.Dequeue();
            var info = element.Current;
            var role = info.ControlType.ProgrammaticName.Replace("ControlType.", "");
            var rect = info.BoundingRectangle;

            if (Roles.Contains(role) && !info.IsOffscreen && !rect.IsEmpty && rect.Width > 1 && rect.Height > 1)
            {
                var label = info.Name?.Trim() ?? "";
                if (label.Length is > 0 and <= 80)
                    lock (output) output.Add(new GuideElement(
                        role, label, new GuideRect(rect.X, rect.Y, rect.Width, rect.Height), appName,
                        StateOf(element, role), window));
            }

            if (role == "Document" || depth >= MaxDepth) continue;
            var child = walker.GetFirstChild(element);
            while (child is not null)
            {
                frontier.Enqueue((child, depth + 1));
                child = walker.GetNextSibling(child);
            }
        }
    }

    private static string? WindowTitle(AutomationElement root)
    {
        try
        {
            var name = root.Current.Name?.Trim();
            if (string.IsNullOrEmpty(name)) return null;
            return name.Length <= 80 ? name : name[..80];
        }
        catch { return null; }
    }

    /// <summary>On, off, or selected. Typed text is never read.</summary>
    private static string? StateOf(AutomationElement element, string role)
    {
        try
        {
            if (role == "CheckBox" &&
                element.TryGetCurrentPattern(TogglePattern.Pattern, out var toggleRaw) &&
                toggleRaw is TogglePattern toggle)
            {
                return toggle.Current.ToggleState switch
                {
                    ToggleState.On => "on",
                    ToggleState.Off => "off",
                    _ => "mixed",
                };
            }
            if (role is "RadioButton" or "TabItem" or "ListItem" or "TreeItem" or "DataItem" &&
                element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectRaw) &&
                selectRaw is SelectionItemPattern item && item.Current.IsSelected)
                return "selected";
        }
        catch { }
        return null;
    }

    private static string AppNameOf(IntPtr hwnd)
    {
        try
        {
            GuideNative.GetWindowThreadProcessId(hwnd, out var pid);
            return Process.GetProcessById((int)pid).ProcessName;
        }
        catch (ArgumentException) { return "App"; }
    }
}
