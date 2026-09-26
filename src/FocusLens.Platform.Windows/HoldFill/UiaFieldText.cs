using System.Windows.Automation;
using FocusLens.Platform.Windows.Native;

namespace FocusLens.Platform.Windows.HoldFill;

/// <summary>
/// Reads whether a field is empty, and types into it.
///
/// Typing uses real key input (Unicode characters, so any language works) after focusing the
/// field, because web pages ignore a value that is set without key events. When the field cannot
/// take focus, UI Automation's SetValue is the fallback.
/// </summary>
public static class UiaFieldText
{
    private const uint KEYEVENTF_UNICODE = 0x0004;

    /// <summary>
    /// True when empty, null when it cannot be told (then the field is never filled). The value is
    /// only compared, never kept. Some apps report their placeholder as the value; that counts as empty.
    /// </summary>
    public static bool? IsEmpty(AutomationElement element)
    {
        var info = element.Current;
        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var vp) && vp is ValuePattern value)
        {
            if (value.Current.IsReadOnly) return null;
            var text = value.Current.Value ?? "";
            return text.Length == 0 || text == info.Name || text == info.HelpText;
        }
        if (element.TryGetCurrentPattern(TextPattern.Pattern, out var tp) && tp is TextPattern textPattern)
            return textPattern.DocumentRange.GetText(1).Length == 0;
        return null;
    }

    /// <summary>Types <paramref name="text"/> into the field. Off the UI thread; never throws.</summary>
    public static Task<bool> TypeAsync(AutomationElement element, string text) => Task.Run(() =>
    {
        try { return Type(element, text); }
        catch { return false; }
    });

    private static bool Type(AutomationElement element, string text)
    {
        // With Ctrl, Alt or Win held, typed letters would become shortcuts in the app.
        if (User32.IsKeyDown(User32.VK_CONTROL) || User32.IsKeyDown(User32.VK_MENU)
            || User32.IsKeyDown(User32.VK_LWIN) || User32.IsKeyDown(User32.VK_RWIN)) return false;

        try { element.SetFocus(); } catch (InvalidOperationException) { }
        Thread.Sleep(80);   // let the app move its caret into the field

        if (HasFocus(element))
        {
            SendUnicode(text);
            return true;
        }
        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var vp) && vp is ValuePattern value && !value.Current.IsReadOnly)
        {
            value.SetValue(text);
            return true;
        }
        return false;
    }

    /// <summary>The field or something inside it (the edit box of a combo box) has keyboard focus.</summary>
    private static bool HasFocus(AutomationElement element)
    {
        var focused = AutomationElement.FocusedElement;
        var walker = TreeWalker.ControlViewWalker;
        for (var i = 0; i < 3 && focused is not null; i++)
        {
            if (Automation.Compare(focused, element)) return true;
            focused = walker.GetParent(focused);
        }
        return false;
    }

    private static void SendUnicode(string text)
    {
        var inputs = new List<User32.INPUT>(text.Length * 2);
        foreach (var unit in text)
        {
            inputs.Add(Key(unit, 0));
            inputs.Add(Key(unit, User32.KEYEVENTF_KEYUP));
        }
        var array = inputs.ToArray();
        User32.SendInput((uint)array.Length, array, System.Runtime.InteropServices.Marshal.SizeOf<User32.INPUT>());
    }

    private static User32.INPUT Key(char unit, uint flags) => new()
    {
        type = User32.INPUT_KEYBOARD,
        ki = new User32.KEYBDINPUT { wVk = 0, wScan = unit, dwFlags = KEYEVENTF_UNICODE | flags },
    };
}
