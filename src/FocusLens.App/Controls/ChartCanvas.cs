using System.Windows;
using System.Windows.Media;
using FocusLens.App.Controls.Charts;
using FocusLens.Core.Ai.Chat;

namespace FocusLens.App.Controls;

/// <summary>Draws a <see cref="ChartPayload"/> (bar, line or pie) with theme-aware colors.</summary>
public sealed class ChartCanvas : FrameworkElement
{
    public static readonly DependencyProperty PayloadProperty = DependencyProperty.Register(
        nameof(Payload), typeof(ChartPayload), typeof(ChartCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public ChartPayload? Payload
    {
        get => (ChartPayload?)GetValue(PayloadProperty);
        set => SetValue(PayloadProperty, value);
    }

    public ChartCanvas()
    {
        // Redraw when the theme dictionary is swapped (brushes are read at render time).
        Loaded += (_, _) => { FocusLens.App.Services.ThemeManager.Changed += Redraw; InvalidateVisual(); };
        Unloaded += (_, _) => FocusLens.App.Services.ThemeManager.Changed -= Redraw;
    }

    private void Redraw() => Dispatcher.BeginInvoke(InvalidateVisual);

    protected override Size MeasureOverride(Size available)
    {
        var height = Payload?.Type switch
        {
            ChartType.Bar => BarRenderer.DesiredHeight(Payload),
            ChartType.Line => LineRenderer.DesiredHeight(Payload),
            ChartType.Pie => PieRenderer.DesiredHeight(Payload),
            _ => 0,
        };
        var width = double.IsInfinity(available.Width) ? 420 : available.Width;
        return new Size(width, height);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Payload is null || Payload.Points.Count == 0) return;

        var dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var text = (Brush)FindResource("TextBrush");
        var muted = (Brush)FindResource("TextMutedBrush");
        var grid = (Brush)FindResource("BorderBrush");
        var size = new Size(ActualWidth, ActualHeight);

        switch (Payload.Type)
        {
            case ChartType.Bar: BarRenderer.Draw(dc, size, Payload, text, muted, dip); break;
            case ChartType.Line: LineRenderer.Draw(dc, size, Payload, text, muted, grid, dip); break;
            case ChartType.Pie: PieRenderer.Draw(dc, size, Payload, text, muted, dip); break;
        }
    }
}
