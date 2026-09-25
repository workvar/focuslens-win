using System.Windows;
using System.Windows.Media;
using FocusLens.Core.Ai.Chat;

namespace FocusLens.App.Controls.Charts;

/// <summary>Horizontal bars with the label on the left and the value on the right.</summary>
internal static class BarRenderer
{
    public static double DesiredHeight(ChartPayload payload) => payload.Points.Count * 28 + 8;

    public static void Draw(DrawingContext dc, Size size, ChartPayload payload, Brush text, Brush muted, double dip)
    {
        var points = payload.Points;
        if (points.Count == 0) return;

        const double labelWidth = 130, valueWidth = 54, rowHeight = 28, barHeight = 14;
        var max = Math.Max(1, points.Max(p => p.Value));
        var barSpace = Math.Max(20, size.Width - labelWidth - valueWidth - 8);

        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            var top = 4 + i * rowHeight;

            var label = ChartPalette.Text(point.Label, 12, text, dip);
            label.MaxTextWidth = labelWidth - 8;
            label.MaxLineCount = 1;
            label.Trimming = TextTrimming.CharacterEllipsis;
            dc.DrawText(label, new Point(0, top + (rowHeight - label.Height) / 2));

            var width = Math.Max(2, barSpace * point.Value / max);
            var rect = new Rect(labelWidth, top + (rowHeight - barHeight) / 2, width, barHeight);
            dc.DrawRoundedRectangle(ChartPalette.Color(point.Color, i), null, rect, 4, 4);

            var value = ChartPalette.Text(ChartPalette.ShortValue(point.Value), 11, muted, dip);
            dc.DrawText(value, new Point(labelWidth + width + 6, top + (rowHeight - value.Height) / 2));
        }
    }
}
