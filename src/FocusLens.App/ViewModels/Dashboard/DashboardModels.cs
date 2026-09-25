using System.Windows.Media;

namespace FocusLens.App.ViewModels.Dashboard;

public sealed record StatCard(string Label, string Value, string Detail);

public sealed record LegendRow(string Name, string Time, string Percent, Brush Color);

public sealed record AppRow(string Name, string Time, double Fraction);

public sealed record HourSegment(double Height, Brush Color);

public sealed record HourBar(string Label, string Tooltip, IReadOnlyList<HourSegment> Segments);

public static class BrushFactory
{
    public static Brush FromHex(string hex)
    {
        try
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
            brush.Freeze();
            return brush;
        }
        catch
        {
            return Brushes.Gray;
        }
    }
}
