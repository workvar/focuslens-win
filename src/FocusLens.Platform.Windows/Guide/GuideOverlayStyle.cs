namespace FocusLens.Platform.Windows.Guide;

/// <summary>
/// Makes the ghost cursor's window invisible to the mouse and to focus. WS_EX_TRANSPARENT lets
/// every click fall through to the app underneath; NOACTIVATE and TOOLWINDOW keep it out of
/// focus, the taskbar and Alt+Tab.
/// </summary>
public static class GuideOverlayStyle
{
    public static void ApplyClickThrough(IntPtr hwnd)
    {
        var style = GuideNative.GetWindowLongPtr(hwnd, GuideNative.GWL_EXSTYLE).ToInt64();
        style |= GuideNative.WS_EX_TRANSPARENT | GuideNative.WS_EX_LAYERED
               | GuideNative.WS_EX_NOACTIVATE | GuideNative.WS_EX_TOOLWINDOW;
        GuideNative.SetWindowLongPtr(hwnd, GuideNative.GWL_EXSTYLE, new IntPtr(style));
    }

    /// <summary>Moves the window's top-left corner to a screen pixel, above other windows, without activating it.</summary>
    public static void MoveToPixel(IntPtr hwnd, int x, int y) =>
        GuideNative.SetWindowPos(hwnd, GuideNative.HWND_TOPMOST, x, y, 0, 0,
            GuideNative.SWP_NOSIZE | GuideNative.SWP_NOACTIVATE);

    /// <summary>The pointer in screen pixels.</summary>
    public static (int X, int Y) CursorPixels()
    {
        GuideNative.GetCursorPos(out var p);
        return (p.X, p.Y);
    }

    public static IntPtr ForegroundWindow() => GuideNative.GetForegroundWindow();

    /// <summary>
    /// Brings a window to the front and gives it keyboard focus, even though this process is not the
    /// one the user is interacting with.
    ///
    /// Windows refuses SetForegroundWindow to a background process, which is exactly the case for the
    /// Guide prompt box: it is opened by a global hotkey while another app is in front. Attaching to
    /// that app's input thread for the moment of the call is the supported way round it. Without it
    /// the box appears without a caret and the user has to click it before they can type, which
    /// defeats the point of a shortcut.
    /// </summary>
    public static void FocusWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        var foreground = GuideNative.GetForegroundWindow();
        if (foreground == hwnd || foreground == IntPtr.Zero)
        {
            GuideNative.SetForegroundWindow(hwnd);
            return;
        }
        var theirThread = GuideNative.GetWindowThreadProcessId(foreground, out _);
        var ourThread = GuideNative.GetCurrentThreadId();
        var attached = theirThread != ourThread && GuideNative.AttachThreadInput(ourThread, theirThread, true);
        try
        {
            GuideNative.SetForegroundWindow(hwnd);
            GuideNative.SetFocus(hwnd);
        }
        finally
        {
            if (attached) GuideNative.AttachThreadInput(ourThread, theirThread, false);
        }
    }
}
