using System.Runtime.InteropServices;
using System.Text;

namespace FocusLens.Platform.Windows.HoldFill;

/// <summary>Win32 calls used by hold to fill: the window under the pointer, its title, and mouse button state.</summary>
public static class HoldFillNative
{
    private const uint GA_ROOT = 2;
    private const int VK_LBUTTON = 0x01, VK_RBUTTON = 0x02, VK_MBUTTON = 0x04;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder text, int max);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);

    /// <summary>The top-level window under a screen pixel.</summary>
    public static IntPtr TopWindowAt(int x, int y)
    {
        var hwnd = WindowFromPoint(new POINT { X = x, Y = y });
        return hwnd == IntPtr.Zero ? IntPtr.Zero : GetAncestor(hwnd, GA_ROOT);
    }

    public static string TitleOf(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return "";
        var sb = new StringBuilder(256);
        GetWindowTextW(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>True while any mouse button is held: dragging or selecting is never "resting".</summary>
    public static bool AnyButtonDown() =>
        Down(VK_LBUTTON) || Down(VK_RBUTTON) || Down(VK_MBUTTON);

    private static bool Down(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
}
