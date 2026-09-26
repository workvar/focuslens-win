using System.Runtime.InteropServices;
using FocusLens.Core.Focus;
using FocusLens.Core.Focus.Classification;
using FocusLens.Core.Focus.Session;
using FocusLens.Platform.Windows.Native;

namespace FocusLens.Platform.Windows.Focus;

/// <summary>
/// Closes the thing the user drifted to: the current tab for a browser, the window for any
/// other app. It never quits an app.
///
/// Safety: the target window is re-read right before closing, and nothing is closed if it now
/// shows a different page, or (for a tab) the browser in front shows a different site or page.
///
///   Browsers         Ctrl+W, sent while that browser window is in front
///   Everything else  WM_CLOSE to the window, the same as clicking its close button
///
/// Windows does not let a normal app send keys or messages to an app running as administrator,
/// so those windows cannot be closed and the user is told so.
/// </summary>
public sealed class WindowsFocusWindowCloser : IFocusWindowCloser
{
    private readonly IFocusContextReader _reader;

    public WindowsFocusWindowCloser(IFocusContextReader reader) => _reader = reader;

    public async Task<FocusCloseOutcome> CloseAsync(FocusContext target)
    {
        var hwnd = new IntPtr(target.Window);
        if (!ShowsSamePage(hwnd, target)) return FocusCloseOutcome.Failed;
        if (!target.IsBrowser) return CloseWindow(hwnd);

        // Ctrl+W goes to the window in front. From the block overlay's button FocusLens may be in
        // front (it cannot always avoid activation), so bring the browser back first. We are the
        // foreground process at that moment, which Windows allows to hand the foreground on.
        if (User32.GetForegroundWindow() != hwnd)
        {
            User32.SetForegroundWindow(hwnd);
            await Task.Delay(150);
            if (User32.GetForegroundWindow() != hwnd) return FocusCloseOutcome.Failed;
        }

        // The browser may be showing a different tab now; never close that one.
        var now = await _reader.CurrentAsync(allowStale: false);
        if (now is null || now.Window != target.Window || !now.IsSameTab(target)) return FocusCloseOutcome.Failed;
        return CloseTab(hwnd);
    }

    /// <summary>
    /// The exact window is still there, owned by the same process, with the same page title. Read
    /// from the window itself rather than the foreground, so the overlay's button works even when
    /// clicking it moved the foreground. Unread counts in titles ("(3) ") are ignored.
    /// </summary>
    private static bool ShowsSamePage(IntPtr hwnd, FocusContext target)
    {
        if (!User32.IsWindow(hwnd)) return false;
        User32.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid != target.Pid) return false;
        return FocusVerdictCache.NormalizedTitle(User32.GetTitle(hwnd)) == FocusVerdictCache.NormalizedTitle(target.WindowTitle);
    }

    private static FocusCloseOutcome CloseTab(IntPtr hwnd)
    {
        // A held Shift would turn Ctrl+W into Ctrl+Shift+W, which closes the whole window.
        if (User32.IsKeyDown(User32.VK_SHIFT) || User32.IsKeyDown(User32.VK_MENU)
            || User32.IsKeyDown(User32.VK_LWIN) || User32.IsKeyDown(User32.VK_RWIN)) return FocusCloseOutcome.Failed;

        var inputs = new[]
        {
            Key(User32.VK_CONTROL, up: false), Key(User32.VK_W, up: false),
            Key(User32.VK_W, up: true), Key(User32.VK_CONTROL, up: true),
        };
        var sent = User32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<User32.INPUT>());
        return sent == inputs.Length ? FocusCloseOutcome.Closed : FocusCloseOutcome.Failed;
    }

    private static FocusCloseOutcome CloseWindow(IntPtr hwnd) =>
        User32.IsWindow(hwnd) && User32.PostMessageW(hwnd, User32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero)
            ? FocusCloseOutcome.Closed
            : FocusCloseOutcome.Failed;

    private static User32.INPUT Key(ushort vk, bool up) => new()
    {
        type = User32.INPUT_KEYBOARD,
        ki = new User32.KEYBDINPUT { wVk = vk, dwFlags = up ? User32.KEYEVENTF_KEYUP : 0 },
    };
}
