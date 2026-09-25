using FocusLens.Platform.Windows.Native;

namespace FocusLens.Platform.Windows.Focus;

/// <summary>
/// Makes a window float without ever taking focus. Clicking the widget or the block overlay
/// must not pull focus off the page the user is on: that page would stop being the foreground
/// window, and closing it would then act on FocusLens instead.
/// </summary>
public static class FloatingWindowStyle
{
    /// <summary>Adds WS_EX_NOACTIVATE and WS_EX_TOOLWINDOW (no taskbar button, no Alt+Tab entry).</summary>
    public static void Apply(IntPtr hwnd)
    {
        var style = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE).ToInt64();
        style |= User32.WS_EX_NOACTIVATE | User32.WS_EX_TOOLWINDOW;
        User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, new IntPtr(style));
    }

    /// <summary>Places the window over a rectangle in screen pixels, above other windows, without activating it.
    /// Pixels, not WPF units, so the overlay lines up on any display scaling.</summary>
    public static void CoverPixels(IntPtr hwnd, int x, int y, int width, int height) =>
        User32.SetWindowPos(hwnd, User32.HWND_TOPMOST, x, y, width, height, User32.SWP_NOACTIVATE);
}
