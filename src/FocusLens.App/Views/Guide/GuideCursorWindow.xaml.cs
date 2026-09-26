using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using System.Windows.Media;
using FocusLens.Platform.Windows.Guide;

namespace FocusLens.App.Views.Guide;

public partial class GuideCursorWindow : Window
{
    /// <summary>Where the arrow's tip sits inside the window, in WPF units, from the near corner.</summary>
    public const double TipInset = 10;
    public const double GlyphWidth = 16;

    private bool _flipped;
    private bool _pointing;

    public GuideCursorWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => GuideOverlayStyle.ApplyClickThrough(Handle);
    }

    public IntPtr Handle => new WindowInteropHelper(this).EnsureHandle();

    public double Scale => VisualTreeHelper.GetDpi(this).DpiScaleX;

    public void SetTag(string text, bool visible)
    {
        TagText.Text = text;
        TagPill.Visibility = visible && text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>True while the ghost rests on a target: shows and animates the pulse ring.</summary>
    public void SetPointing(bool pointing)
    {
        if (_pointing == pointing) return;
        _pointing = pointing;
        RingScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        RingScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        Ring.BeginAnimation(OpacityProperty, null);

        if (!pointing) { Ring.Opacity = 0; return; }
        // Reduced motion: a still ring, no animation.
        if (!SystemParameters.ClientAreaAnimation) { Ring.Opacity = 0.6; return; }

        var grow = new DoubleAnimation(0.9, 1.35, TimeSpan.FromSeconds(1.1)) { RepeatBehavior = RepeatBehavior.Forever };
        RingScale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        RingScale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        Ring.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0.8, 0.15, TimeSpan.FromSeconds(1.1)) { RepeatBehavior = RepeatBehavior.Forever });
    }

    /// <summary>Puts the tag left of the arrow, so it is not cut off at the right edge of the screen.</summary>
    public void SetFlipped(bool flipped)
    {
        if (_flipped == flipped) return;
        _flipped = flipped;
        Root.FlowDirection = flipped ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Root.HorizontalAlignment = flipped ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    }

    /// <summary>Moves the window so the arrow tip lands on this screen pixel.</summary>
    public void PlaceTip(double tipX, double tipY)
    {
        var scale = Scale;
        var virtualRight = (SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth) * scale;
        SetFlipped(tipX + (Width - TipInset) * scale > virtualRight);

        var x = _flipped ? tipX - (Width - TipInset - GlyphWidth) * scale : tipX - TipInset * scale;
        var y = tipY - TipInset * scale;
        GuideOverlayStyle.MoveToPixel(Handle, (int)Math.Round(x), (int)Math.Round(y));
    }
}
