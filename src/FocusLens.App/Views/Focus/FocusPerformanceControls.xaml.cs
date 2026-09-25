using System.Windows;
using System.Windows.Controls;

namespace FocusLens.App.Views.Focus;

public partial class FocusPerformanceControls : UserControl
{
    /// <summary>Shows the separate focus-check model field. Off on the Focus page, on in Settings.</summary>
    public static readonly DependencyProperty ShowsModelProperty =
        DependencyProperty.Register(nameof(ShowsModel), typeof(bool), typeof(FocusPerformanceControls), new PropertyMetadata(true));

    public FocusPerformanceControls() => InitializeComponent();

    public bool ShowsModel
    {
        get => (bool)GetValue(ShowsModelProperty);
        set => SetValue(ShowsModelProperty, value);
    }
}
