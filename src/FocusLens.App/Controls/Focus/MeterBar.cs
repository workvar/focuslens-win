using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;

namespace FocusLens.App.Controls.Focus;

/// <summary>
/// The patience bar, and the plain bars in the insights breakdown. Full and green while you are
/// on track, draining through amber to red while you are off topic. Set Fill to use one colour.
/// </summary>
public sealed class MeterBar : ThemedElement
{
    public static readonly DependencyProperty LevelProperty =
        DependencyProperty.Register(nameof(Level), typeof(double), typeof(MeterBar), new FrameworkPropertyMetadata(
            1.0, FrameworkPropertyMetadataOptions.AffectsRender,
            (d, e) => AutomationProperties.SetHelpText(d, $"{Math.Round((double)e.NewValue * 100)} percent")));

    public static readonly DependencyProperty FillProperty =
        DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(MeterBar), Affects(null!));

    public MeterBar()
    {
        Height = 8;
        AutomationProperties.SetName(this, "Patience before action");
    }

    /// <summary>1 is full, 0 is empty.</summary>
    public double Level
    {
        get => (double)GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var height = ActualHeight;
        var radius = height / 2;
        dc.DrawRoundedRectangle(Themed("SurfaceAltBrush"), null, new Rect(0, 0, ActualWidth, height), radius, radius);

        var level = Math.Clamp(Level, 0, 1);
        if (level <= 0) return;
        var width = Math.Max(height, ActualWidth * level);
        dc.DrawRoundedRectangle(Fill ?? LevelBrush(level), null, new Rect(0, 0, width, height), radius, radius);
    }
}
