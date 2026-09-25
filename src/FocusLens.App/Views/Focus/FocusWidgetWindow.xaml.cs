using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Effects;
using FocusLens.Platform.Windows.Focus;

namespace FocusLens.App.Views.Focus;

/// <summary>The floating widget. It never takes focus, so clicking it leaves the page you are on in front.</summary>
public partial class FocusWidgetWindow : Window
{
    public FocusWidgetWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => FloatingWindowStyle.Apply(new WindowInteropHelper(this).Handle);
    }

    /// <summary>Raised after the user drags the widget somewhere new.</summary>
    public event Action? Moved;

    /// <summary>A soft shadow unless visual effects are reduced.</summary>
    public void SetShadow(bool on) =>
        Card.Effect = on ? new DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Opacity = 0.25, Direction = 270 } : null;

    /// <summary>Drag from any empty part of the card.</summary>
    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        var before = new Point(Left, Top);
        DragMove();
        if (new Point(Left, Top) != before) Moved?.Invoke();
    }
}
