using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace FocusLens.App.Services;

/// <summary>Turns the Windows 11 acrylic backdrop on or off for one window and matches its title bar to the theme.</summary>
public static class WindowBackdrop
{
    /// <summary>The system backdrop attribute is honored from Windows 11 22H2 (build 22621).</summary>
    public static bool IsSupported => Environment.OSVersion.Version.Build >= 22621;

    public static void Apply(Window window, bool dark, bool glass)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var useGlass = glass && IsSupported;

        var titleBar = dark ? 1 : 0;
        DwmInterop.DwmSetWindowAttribute(hwnd, DwmInterop.UseImmersiveDarkMode, ref titleBar, sizeof(int));

        var backdrop = useGlass ? DwmInterop.BackdropAcrylic : DwmInterop.BackdropNone;
        DwmInterop.DwmSetWindowAttribute(hwnd, DwmInterop.SystemBackdropType, ref backdrop, sizeof(int));

        // The backdrop only shows through where the window is transparent, so glass extends the frame
        // over the whole client area and lets WPF paint with a transparent clear color.
        var margins = useGlass ? new DwmInterop.Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 } : default;
        DwmInterop.DwmExtendFrameIntoClientArea(hwnd, ref margins);

        // Flat mode paints the theme's window color, not the system one, so dark mode has no light flash.
        var flat = Application.Current.TryFindResource("WindowColor") is Color themed ? themed : SystemColors.WindowColor;
        if (HwndSource.FromHwnd(hwnd)?.CompositionTarget is { } target)
            target.BackgroundColor = useGlass ? Colors.Transparent : flat;
    }
}
