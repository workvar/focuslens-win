using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FocusLens.App.ViewModels.Dashboard;

namespace FocusLens.App.Controls.Charts;

/// <summary>Shared drawing helpers for the chart renderers.</summary>
internal static class ChartPalette
{
    private static readonly string[] Fallback =
    {
        "#1A56A0", "#0E8A7A", "#7C3AED", "#DC2626", "#D97706", "#64748B", "#0EA5E9", "#9CA3AF",
    };

    public static Brush Color(string? hex, int index) =>
        BrushFactory.FromHex(string.IsNullOrWhiteSpace(hex) ? Fallback[index % Fallback.Length] : hex);

    public static FormattedText Text(string text, double size, Brush brush, double pixelsPerDip, FontWeight? weight = null) =>
        new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal),
            size, brush, pixelsPerDip);

    public static string ShortValue(double seconds) =>
        seconds >= 3600 ? $"{seconds / 3600:0.0}h" : seconds >= 60 ? $"{seconds / 60:0}m" : $"{seconds:0}s";
}
