using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;
using FocusLens.Core.HoldFill;
using FocusLens.Platform.Windows.Guide;

namespace FocusLens.App.Views.HoldFill;

public partial class HoldFillBubble : Window
{
    /// <summary>Screen pixels between the pointer and the bubble, so it never covers the field's text.</summary>
    private const double OffsetX = 18, OffsetY = 22;
    private const double RingRadius = 11.5, RingCenter = 13;

    private HoldFillStage? _stage;
    private double _progress = -1;

    public HoldFillBubble()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => GuideOverlayStyle.ApplyClickThrough(Handle);
    }

    private IntPtr Handle => new WindowInteropHelper(this).EnsureHandle();

    public void Present(HoldFillStage stage, double progress, string? suggestion)
    {
        if (!IsVisible) Show();
        if (_stage != stage)
        {
            _stage = stage;
            Caption.Text = stage switch
            {
                HoldFillStage.Suggesting => "Finding a search...",
                HoldFillStage.Holding => "Hold still to fill",
                _ => "Filled",
            };
            Check.Visibility = stage == HoldFillStage.Filled ? Visibility.Visible : Visibility.Collapsed;
            // Screen readers hear the suggestion once, when it appears.
            AutomationProperties.SetName(this, stage == HoldFillStage.Holding ? $"Hold to fill: {suggestion}" : Caption.Text);
        }
        if (Suggestion.Text != (suggestion ?? ""))
        {
            Suggestion.Text = suggestion ?? "";
            Suggestion.Visibility = string.IsNullOrEmpty(suggestion) ? Visibility.Collapsed : Visibility.Visible;
            // Resize to the new text now, not on the next layout pass, or the first frame is cut off.
            UpdateLayout();
        }
        SetProgress(stage == HoldFillStage.Filled ? 1 : progress);
    }

    /// <summary>Beside the pointer, flipped left or up near the edge of the screen. Screen pixels.</summary>
    public void PlaceNear(int pointerX, int pointerY)
    {
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var width = ActualWidth * scale;
        var height = ActualHeight * scale;
        var right = (SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth) * scale;
        var bottom = (SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight) * scale;

        var x = pointerX + OffsetX * scale;
        var y = pointerY + OffsetY * scale;
        if (x + width > right) x = pointerX - OffsetX * scale - width;
        if (y + height > bottom) y = pointerY - OffsetY * scale - height;
        GuideOverlayStyle.MoveToPixel(Handle, (int)Math.Round(x), (int)Math.Round(y));
    }

    private void SetProgress(double progress)
    {
        progress = Math.Clamp(progress, 0, 1);
        if (Math.Abs(progress - _progress) < 0.004) return;
        _progress = progress;
        Arc.Data = ArcGeometry(progress);
    }

    /// <summary>A clockwise arc from twelve o'clock. A full ring is drawn as an ellipse.</summary>
    private static Geometry ArcGeometry(double progress)
    {
        if (progress <= 0) return Geometry.Empty;
        if (progress >= 0.999) return new EllipseGeometry(new Point(RingCenter, RingCenter), RingRadius, RingRadius);

        var angle = progress * 2 * Math.PI;
        var start = new Point(RingCenter, RingCenter - RingRadius);
        var end = new Point(RingCenter + RingRadius * Math.Sin(angle), RingCenter - RingRadius * Math.Cos(angle));
        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new ArcSegment(end, new Size(RingRadius, RingRadius), 0, progress > 0.5, SweepDirection.Clockwise, true));
        var geometry = new PathGeometry(new[] { figure });
        geometry.Freeze();
        return geometry;
    }
}
