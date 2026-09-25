using FocusLens.Platform.Windows.Native;

namespace FocusLens.Platform.Windows.Capture;

/// <summary>
/// Chromium based windows (WebView2, Electron, Chrome) build their accessibility tree only after a
/// screen reader style client asks for it. Asking once per window makes apps like WhatsApp expose
/// their text to UI Automation; the tree fills in shortly after, so the next read sees it.
/// </summary>
internal static class AccessibilityNudge
{
    private const int ObjIdClient = -4;
    private const int UiaRootObjectId = -25;
    private const int MaxTracked = 500;

    private static readonly HashSet<IntPtr> Done = new();
    private static readonly object Gate = new();

    public static void Apply(IntPtr hwnd)
    {
        lock (Gate)
        {
            if (Done.Count > MaxTracked) Done.Clear();
            if (!Done.Add(hwnd)) return;
        }

        try
        {
            Ask(hwnd);
            User32.EnumChildWindows(hwnd, (child, _) =>
            {
                if (User32.GetClassName(child).StartsWith("Chrome_", StringComparison.Ordinal)) Ask(child);
                return true;
            }, IntPtr.Zero);
        }
        catch
        {
            // Best effort only; a window that vanishes mid-call is fine.
        }
    }

    private static void Ask(IntPtr window)
    {
        User32.SendMessageTimeoutW(window, User32.WM_GETOBJECT, IntPtr.Zero, new IntPtr(ObjIdClient),
            User32.SMTO_ABORTIFHUNG, 200, out _);
        User32.SendMessageTimeoutW(window, User32.WM_GETOBJECT, IntPtr.Zero, new IntPtr(UiaRootObjectId),
            User32.SMTO_ABORTIFHUNG, 200, out _);
    }
}
