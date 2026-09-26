using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FocusLens.App.Views.Guide;

public partial class GuidePromptWindow : Window
{
    public event Action<string>? Submitted;

    /// <summary>
    /// Closing an active window deactivates it, and Deactivated closes the box. Without this guard
    /// that second Close threw "cannot ... while a Window is closing", the exception unwound through
    /// the Enter handler, and the request was never submitted.
    /// </summary>
    private bool _closing;

    public GuidePromptWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => Input.Focus();
        Deactivated += (_, _) => CloseOnce();
        Closing += (_, _) => _closing = true;
    }

    /// <summary>Opens the box next to the pointer, kept inside the screen. Position is in WPF units.</summary>
    public void ShowNear(double pointerX, double pointerY)
    {
        Left = Math.Clamp(pointerX - Width / 2, SystemParameters.VirtualScreenLeft + 8,
            SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width - 8);
        Top = Math.Clamp(pointerY + 24, SystemParameters.VirtualScreenTop + 8,
            SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 96);
        Show();
        Activate();
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e) =>
        Placeholder.Visibility = Input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { CloseOnce(); e.Handled = true; }
        else if (e.Key == Key.Enter)
        {
            var text = Input.Text;
            e.Handled = true;
            CloseOnce();
            if (text.Trim().Length > 0) Submitted?.Invoke(text);
        }
    }

    public void CloseOnce()
    {
        if (_closing) return;
        _closing = true;
        Close();
    }
}
