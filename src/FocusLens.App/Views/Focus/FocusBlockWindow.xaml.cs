using System.Windows;
using System.Windows.Interop;
using FocusLens.Core.Focus;
using FocusLens.Platform.Windows.Focus;

namespace FocusLens.App.Views.Focus;

/// <summary>
/// The block overlay. It never takes focus: the covered window stays the foreground window, so
/// "Close this tab" acts on it and not on FocusLens.
/// </summary>
public partial class FocusBlockWindow : Window
{
    public FocusBlockWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => FloatingWindowStyle.Apply(new WindowInteropHelper(this).Handle);
    }

    /// <summary>Covers <paramref name="bounds"/> (screen pixels) exactly, on any display scaling.</summary>
    public void Cover(FocusRect bounds)
    {
        var handle = new WindowInteropHelper(this).EnsureHandle();
        FloatingWindowStyle.CoverPixels(handle, bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }
}
