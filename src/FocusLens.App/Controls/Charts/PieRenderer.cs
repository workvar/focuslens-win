using System.Windows;
using System.Windows.Media;
using FocusLens.Core.Ai.Chat;

namespace FocusLens.App.Controls.Charts;

/// <summary>A donut chart of shares of a whole.</summary>
internal static class PieRenderer
{
    public static double DesiredHeight(ChartPayload payload) => 200;

    public static void Draw(DrawingContext dc, Size size, ChartPayload payload, Brush text, Brush muted, double dip)
    {
        var points = payload.Points.Where(p => p.Value > 0).ToList();
        var total = points.Sum(p => p.Value);
        if (total <= 0) return;

        var diameter = Math.Min(size.Height - 8, size.Width * 0.5);
        var radius = diameter / 2;
        var center = new Point(radius + 4, size.Height / 2);
        var inner = radius * 0.62;

        var angle = -90.0;
        for (var i = 0; i < points.Count; i++)
        {
            var sweep = points.Count == 1 ? 359.99 : 360.0 * points[i].Value / total;
            dc.DrawGeometry(ChartPalette.Color(points[i].Color, i), null, Slice(center, radius, inner, angle, sweep));
            angle += sweep;
        }

        var legendX = center.X + radius + 20;
        for (var i = 0; i < points.Count && i < 8; i++)
        {
            var y = 10 + i * 22;
            dc.DrawRoundedRectangle(ChartPalette.Color(points[i].Color, i), null, new Rect(legendX, y + 3, 10, 10), 3, 3);
            var label = ChartPalette.Text($"{points[i].Label}  {points[i].Value * 100 / total:0}%", 12, text, dip);
            label.MaxTextWidth = Math.Max(40, size.Width - legendX - 20);
            label.MaxLineCount = 1;
            label.Trimming = TextTrimming.CharacterEllipsis;
            dc.DrawText(label, new Point(legendX + 16, y));
        }
    }

    private static Geometry Slice(Point center, double outer, double inner, double startDeg, double sweepDeg)
    {
        Point At(double radius, double deg)
        {
            var rad = deg * Math.PI / 180;
            return new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));
        }

        var large = sweepDeg > 180;
        var end = startDeg + sweepDeg;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(At(outer, startDeg), true, true);
            ctx.ArcTo(At(outer, end), new Size(outer, outer), 0, large, SweepDirection.Clockwise, true, false);
            ctx.LineTo(At(inner, end), true, false);
            ctx.ArcTo(At(inner, startDeg), new Size(inner, inner), 0, large, SweepDirection.Counterclockwise, true, false);
        }
        geometry.Freeze();
        return geometry;
    }
}
