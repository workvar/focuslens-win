using System.Windows;
using System.Windows.Media;
using FocusLens.App.Services;

namespace FocusLens.App.Controls.Focus;

/// <summary>
/// Base for the custom-drawn focus controls: redraws when the theme changes (brushes are read
/// at render time) and looks up theme brushes by key.
/// </summary>
public abstract class ThemedElement : FrameworkElement
{
    protected ThemedElement()
    {
        Loaded += (_, _) => { ThemeManager.Changed += Redraw; InvalidateVisual(); };
        Unloaded += (_, _) => ThemeManager.Changed -= Redraw;
    }

    private void Redraw() => Dispatcher.BeginInvoke(InvalidateVisual);

    protected Brush Themed(string key) => TryFindResource(key) as Brush ?? Brushes.Gray;

    /// <summary>Green from 70, amber from 40, red below. Used by the ring.</summary>
    protected Brush ScoreBrush(int score) =>
        Themed(score >= 70 ? "SuccessBrush" : score >= 40 ? "WarningBrush" : "DangerBrush");

    /// <summary>Green over half, amber over a quarter, red below. Used by the patience bar.</summary>
    protected Brush LevelBrush(double level) =>
        Themed(level > 0.5 ? "SuccessBrush" : level > 0.25 ? "WarningBrush" : "DangerBrush");

    protected static FrameworkPropertyMetadata Affects(object defaultValue) =>
        new(defaultValue, FrameworkPropertyMetadataOptions.AffectsRender);
}
