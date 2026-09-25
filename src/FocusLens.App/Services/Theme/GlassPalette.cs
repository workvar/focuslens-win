using System.Windows;
using System.Windows.Media;

namespace FocusLens.App.Services;

/// <summary>
/// Makes the big surfaces translucent for the glass look by shadowing three theme colors with
/// lower-alpha copies in the application dictionary. Removing the copies restores the flat theme.
/// </summary>
internal static class GlassPalette
{
    /// <summary>How much alpha each layer loses at full transparency; the window shows the most backdrop.</summary>
    private static readonly (string Key, double MaxReduction)[] Layers =
    {
        ("WindowColor", 0.70),
        ("SurfaceColor", 0.55),
        ("SurfaceAltColor", 0.45),
    };

    public static void Apply(ResourceDictionary resources, bool glass, double transparency)
    {
        // Clear old overrides first so the base color below is always read from the theme dictionary.
        foreach (var (key, _) in Layers) resources.Remove(key);
        if (!glass) return;

        var amount = Math.Clamp(transparency, 0, 1);
        foreach (var (key, maxReduction) in Layers)
        {
            if (Application.Current.TryFindResource(key) is not Color baseColor) continue;
            var alpha = (byte)Math.Round(255 * (1 - maxReduction * amount));
            resources[key] = Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);
        }
    }
}
