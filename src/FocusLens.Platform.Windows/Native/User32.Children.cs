using System.Runtime.InteropServices;
using System.Text;

namespace FocusLens.Platform.Windows.Native;

internal static partial class User32
{
    public const uint WM_GETOBJECT = 0x003D;
    public const uint SMTO_ABORTIFHUNG = 0x0002;

    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassNameW(IntPtr hWnd, StringBuilder name, int maxCount);
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SendMessageTimeoutW(
        IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeoutMs, out IntPtr result);

    public static string GetClassName(IntPtr hWnd)
    {
        var builder = new StringBuilder(256);
        return GetClassNameW(hWnd, builder, builder.Capacity) > 0 ? builder.ToString() : "";
    }
}
