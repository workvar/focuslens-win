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
}
