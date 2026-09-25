using System.Windows;
using System.Windows.Media;

namespace FocusLens.App.Services;

/// <summary>
/// Rebuilds the theme brushes from the current theme colors and writes them into the application
/// dictionary. Brushes declared in XAML as Color="{DynamicResource ...}" are resolved once and do not
/// follow a swapped palette dictionary, so the surfaces and text stayed light while only the title
/// bar went dark. Replacing the brush entries notifies every DynamicResource consumer.
/// </summary>
internal static class BrushPalette
{
    private static readonly (string Brush, string Color)[] Map =
    {
        ("WindowBrush", "WindowColor"),
        ("SurfaceBrush", "SurfaceColor"),
        ("SurfaceAltBrush", "SurfaceAltColor"),
        ("BorderBrush", "BorderColor"),
        ("TextBrush", "TextColor"),
        ("TextMutedBrush", "TextMutedColor"),
        ("AccentBrush", "AccentColor"),
        ("AccentHoverBrush", "AccentHoverColor"),
        ("AccentTextBrush", "AccentTextColor"),
        ("AccentSoftBrush", "AccentSoftColor"),
        ("DangerBrush", "DangerColor"),
        ("SuccessBrush", "SuccessColor"),
        ("WarningBrush", "WarningColor"),
    };

    /// <summary>Call after the palette swap and the glass overlay so the brushes see the final colors.</summary>
    public static void Apply(ResourceDictionary resources)
    {
        foreach (var (brushKey, colorKey) in Map)
        {
            if (Application.Current.TryFindResource(colorKey) is not Color color) continue;
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            resources[brushKey] = brush;
        }
    }
}
