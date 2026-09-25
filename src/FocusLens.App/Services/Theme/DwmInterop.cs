using System.Runtime.InteropServices;

namespace FocusLens.App.Services;

/// <summary>The few dwmapi calls needed for a dark title bar and the acrylic window backdrop.</summary>
internal static class DwmInterop
{
    public const int UseImmersiveDarkMode = 20;
    public const int SystemBackdropType = 38;

    public const int BackdropNone = 1;
    public const int BackdropAcrylic = 3;

    [StructLayout(LayoutKind.Sequential)]
    public struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);
}
