using System.Windows;
using System.Windows.Media;
using FocusLens.Core.Ai.Chat;

namespace FocusLens.App.Controls.Charts;

/// <summary>A simple line chart with point markers and x-axis labels.</summary>
internal static class LineRenderer
{
    public static double DesiredHeight(ChartPayload payload) => 170;

    public static void Draw(DrawingContext dc, Size size, ChartPayload payload, Brush text, Brush muted, Brush grid, double dip)
    {
        var points = payload.Points;
        if (points.Count < 2) return;

        const double left = 34, bottom = 22, top = 8, right = 10;
        var plot = new Rect(left, top, Math.Max(10, size.Width - left - right), Math.Max(10, size.Height - top - bottom));
        var max = Math.Max(1, points.Max(p => p.Value));
        var min = Math.Min(0, points.Min(p => p.Value));

        var gridPen = new Pen(grid, 1);
        for (var i = 0; i <= 3; i++)
        {
            var y = plot.Bottom - plot.Height * i / 3.0;
            dc.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            var tick = ChartPalette.Text($"{min + (max - min) * i / 3.0:0}", 10, muted, dip);
            dc.DrawText(tick, new Point(0, y - tick.Height / 2));
        }

        var color = ChartPalette.Color(points[0].Color, 0);
        var stepX = plot.Width / (points.Count - 1);
        Point Position(int index) =>
            new(plot.Left + stepX * index, plot.Bottom - plot.Height * (points[index].Value - min) / (max - min));

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(Position(0), false, false);
            for (var i = 1; i < points.Count; i++) ctx.LineTo(Position(i), true, true);
        }
        geometry.Freeze();
        dc.DrawGeometry(null, new Pen(color, 2.5) { LineJoin = PenLineJoin.Round }, geometry);

        for (var i = 0; i < points.Count; i++)
        {
            var p = Position(i);
            dc.DrawEllipse(color, null, p, 3.5, 3.5);
            var label = ChartPalette.Text(points[i].Label, 10, muted, dip);
            dc.DrawText(label, new Point(p.X - label.Width / 2, plot.Bottom + 4));
        }
    }
}
