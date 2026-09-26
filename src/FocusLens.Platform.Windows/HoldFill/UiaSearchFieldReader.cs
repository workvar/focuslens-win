using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using FocusLens.Core.Guide;
using FocusLens.Core.HoldFill;

namespace FocusLens.Platform.Windows.HoldFill;

/// <summary>A search field and the UI Automation element to type into.</summary>
public sealed record FoundSearchField(SearchField Info, AutomationElement Element);

/// <summary>
/// Finds the search field under the pointer through UI Automation, off the UI thread and with a
/// time limit, so a busy app costs a missed offer, never a stall.
///
/// Reads the field's name, help text and automation id, and only whether it is empty; the text
/// in it is never kept or sent anywhere. Password fields and FocusLens's own windows are skipped.
/// </summary>
public sealed class UiaSearchFieldReader
{
    public static readonly TimeSpan LookupTimeout = TimeSpan.FromMilliseconds(900);

    /// <summary>The element under the pointer is often the text inside the box, so look a few parents up.</summary>
    private const int MaxParents = 4;

    private readonly int _ownPid = Environment.ProcessId;

    /// <summary>Screen pixels. Never throws.</summary>
    public async Task<FoundSearchField?> ReadAtAsync(int x, int y)
    {
        var read = Task.Run(() =>
        {
            try { return Read(x, y); }
            catch { return null; }   // the window closed, or the app refused UI Automation
        });
        try { return await read.WaitAsync(LookupTimeout); }
        catch (TimeoutException) { return null; }
    }

    private FoundSearchField? Read(int x, int y)
    {
        var top = HoldFillNative.TopWindowAt(x, y);
        var element = AutomationElement.FromPoint(new Point(x, y));
        var walker = TreeWalker.ControlViewWalker;

        for (var i = 0; i <= MaxParents && element is not null && element != AutomationElement.RootElement; i++)
        {
            var info = element.Current;
            if (info.ProcessId == _ownPid) return null;
            if (Candidate(element, info) is { } field)
                return new FoundSearchField(field with { WindowTitle = HoldFillNative.TitleOf(top) }, element);
            element = walker.GetParent(element);
        }
        return null;
    }

    private static SearchField? Candidate(AutomationElement element, AutomationElement.AutomationElementInformation info)
    {
        if (info.ControlType != ControlType.Edit && info.ControlType != ControlType.ComboBox) return null;
        if (info.IsPassword || !info.IsEnabled || !info.IsKeyboardFocusable) return null;
        if (!SearchFieldRules.IsSearchLike(info.Name, info.HelpText, info.AutomationId, info.ClassName, info.LocalizedControlType))
            return null;

        var rect = info.BoundingRectangle;
        if (rect.IsEmpty || rect.Width < 8 || rect.Height < 8) return null;
        if (UiaFieldText.IsEmpty(element) is not { } empty) return null;   // cannot tell: never overwrite

        return new SearchField(
            Id: string.Join(".", element.GetRuntimeId()),
            Frame: new GuideRect(rect.X, rect.Y, rect.Width, rect.Height),
            AppName: AppName(info.ProcessId),
            Label: SearchFieldRules.LabelFrom(info.Name, info.HelpText),
            WindowTitle: "",
            IsEmpty: empty);
    }

    private static string AppName(int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            try
            {
                var description = process.MainModule?.FileVersionInfo.FileDescription;
                if (!string.IsNullOrWhiteSpace(description)) return description;
            }
            catch { /* elevated or 32-bit: fall back to the process name */ }
            return process.ProcessName;
        }
        catch { return "App"; }
    }
}
