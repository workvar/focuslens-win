using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using FocusLens.App.Controls.Charts;
using FocusLens.Core.Focus;

namespace FocusLens.App.Controls.Focus;

/// <summary>The live score over the session, with the stretches spent off topic shaded behind it.</summary>
public sealed class FocusScoreChart : ThemedElement
{
    public static readonly DependencyProperty RecordProperty =
        DependencyProperty.Register(nameof(Record), typeof(FocusSessionRecord), typeof(FocusScoreChart), Affects(null!));

    private const double Left = 34, Bottom = 34, Top = 8, Right = 10;

    public FocusScoreChart()
    {
        Height = 200;
        AutomationProperties.SetName(this, "Focus score over time");
    }

    public FocusSessionRecord? Record
    {
        get => (FocusSessionRecord?)GetValue(RecordProperty);
        set => SetValue(RecordProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Record is not { } record) return;
        var dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var muted = Themed("TextMutedBrush");
        var plot = new Rect(Left, Top, Math.Max(10, ActualWidth - Left - Right), Math.Max(10, ActualHeight - Top - Bottom));
        var minutes = Math.Max(record.Duration() / 60, record.Timeline.Select(p => p.Offset / 60).DefaultIfEmpty(0).Max());
        minutes = Math.Max(minutes, 0.1);

        double X(double minute) => plot.Left + plot.Width * Math.Clamp(minute / minutes, 0, 1);
        double Y(double score) => plot.Bottom - plot.Height * Math.Clamp(score, 0, 100) / 100;

        DrawEpisodes(dc, record, plot, X);
        DrawGrid(dc, plot, Y, muted, dip);
        DrawScore(dc, record, X, Y, plot);

        var label = ChartPalette.Text("Minutes into the session", 10, muted, dip);
        dc.DrawText(label, new Point(plot.Left + (plot.Width - label.Width) / 2, plot.Bottom + 18));
        var end = ChartPalette.Text($"{minutes:0}", 10, muted, dip);
        dc.DrawText(end, new Point(plot.Right - end.Width, plot.Bottom + 3));
        dc.DrawText(ChartPalette.Text("0", 10, muted, dip), new Point(plot.Left, plot.Bottom + 3));
    }

    private void DrawEpisodes(DrawingContext dc, FocusSessionRecord record, Rect plot, Func<double, double> x)
    {
        var shade = Themed("DangerBrush").Clone();
        shade.Opacity = 0.14;
        foreach (var episode in record.Episodes)
        {
            var start = (episode.StartedAt - record.StartedAt).TotalMinutes;
            var left = x(start);
            var right = Math.Max(left + 1, x(start + episode.Seconds / 60));
            dc.DrawRectangle(shade, null, new Rect(left, plot.Top, right - left, plot.Height));
        }
    }

    private void DrawGrid(DrawingContext dc, Rect plot, Func<double, double> y, Brush muted, double dip)
    {
        var pen = new Pen(Themed("BorderBrush"), 1);
        foreach (var score in new[] { 0, 50, 100 })
        {
            var at = y(score);
            dc.DrawLine(pen, new Point(plot.Left, at), new Point(plot.Right, at));
            var tick = ChartPalette.Text(score.ToString(), 10, muted, dip);
            dc.DrawText(tick, new Point(Left - tick.Width - 6, at - tick.Height / 2));
        }
    }

    private void DrawScore(DrawingContext dc, FocusSessionRecord record, Func<double, double> x, Func<double, double> y, Rect plot)
    {
        var points = record.Timeline.Select(p => new Point(x(p.Offset / 60), y(p.Score))).ToList();
        if (points.Count < 2) return;

        var accent = Themed("AccentBrush");
        var area = new StreamGeometry();
        using (var ctx = area.Open())
        {
            ctx.BeginFigure(new Point(points[0].X, plot.Bottom), true, true);
            foreach (var p in points) ctx.LineTo(p, true, false);
            ctx.LineTo(new Point(points[^1].X, plot.Bottom), true, false);
        }
        area.Freeze();
        var fill = accent.Clone();
        fill.Opacity = 0.15;
        dc.DrawGeometry(fill, null, area);

        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(points[0], false, false);
            foreach (var p in points.Skip(1)) ctx.LineTo(p, true, true);
        }
        line.Freeze();
        dc.DrawGeometry(null, new Pen(accent, 2.5) { LineJoin = PenLineJoin.Round }, line);
    }
}
