using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;

namespace FocusLens.App.Controls.Focus;

/// <summary>A ring with the focus score in the middle. Size it with Width and Height.</summary>
public sealed class ScoreRing : ThemedElement
{
    public static readonly DependencyProperty ScoreProperty =
        DependencyProperty.Register(nameof(Score), typeof(int), typeof(ScoreRing), new FrameworkPropertyMetadata(
            100, FrameworkPropertyMetadataOptions.AffectsRender,
            (d, e) => AutomationProperties.SetHelpText(d, $"{e.NewValue} out of 100")));

    public static readonly DependencyProperty ThicknessProperty =
        DependencyProperty.Register(nameof(Thickness), typeof(double), typeof(ScoreRing), Affects(6.0));

    public ScoreRing() => AutomationProperties.SetName(this, "Focus score");

    public int Score
    {
        get => (int)GetValue(ScoreProperty);
        set => SetValue(ScoreProperty, value);
    }

    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= Thickness * 2) return;

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radius = (size - Thickness) / 2;
        dc.DrawEllipse(null, new Pen(Themed("SurfaceAltBrush"), Thickness), center, radius, radius);

        var fraction = Math.Clamp(Score, 0, 100) / 100.0;
        if (fraction > 0) dc.DrawGeometry(null, ArcPen(), Arc(center, radius, fraction));

        var dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var text = new FormattedText(Score.ToString(CultureInfo.CurrentCulture), CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal,
                FontWeights.Bold, FontStretches.Normal), size * 0.32, Themed("TextBrush"), dip);
        dc.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
    }

    private Pen ArcPen() => new(ScoreBrush(Score), Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

    /// <summary>Clockwise from twelve o'clock. A full score is a whole circle, since one arc cannot close on itself.</summary>
    private static Geometry Arc(Point center, double radius, double fraction)
    {
        if (fraction >= 0.999)
            return new EllipseGeometry(center, radius, radius);

        var angle = fraction * 2 * Math.PI;
        var start = new Point(center.X, center.Y - radius);
        var end = new Point(center.X + radius * Math.Sin(angle), center.Y - radius * Math.Cos(angle));
        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new ArcSegment(end, new Size(radius, radius), 0, fraction > 0.5, SweepDirection.Clockwise, true));
        var geometry = new PathGeometry(new[] { figure });
        geometry.Freeze();
        return geometry;
    }
}
