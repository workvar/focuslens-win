using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FocusLens.App.ViewModels.Focus;

namespace FocusLens.App.Converters;

/// <summary>
/// Maps a <see cref="FocusTone"/> to the matching theme brush. Muted maps to TextMutedBrush,
/// or to TextBrush when the parameter is "text" (stat tiles show plain numbers in the text colour).
/// Set Soft for a faint tint of the colour, used behind badges and the widget drawer.
/// </summary>
public sealed class FocusToneToBrushConverter : IValueConverter
{
    public bool Soft { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value switch
        {
            FocusTone.Accent => "AccentBrush",
            FocusTone.Success => "SuccessBrush",
            FocusTone.Warning => "WarningBrush",
            FocusTone.Danger => "DangerBrush",
            _ => parameter as string == "text" ? "TextBrush" : "TextMutedBrush",
        };
        var brush = Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
        if (!Soft) return brush;
        var soft = brush.Clone();
        soft.Opacity = 0.14;
        soft.Freeze();
        return soft;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
