using System.Windows;
using FocusLens.App.Services;

namespace FocusLens.App.Views;

/// <summary>Themed yes/no prompt. Cancel is the default button so a stray Enter never deletes anything.</summary>
public partial class ConfirmDialogWindow : Window
{
    public ConfirmDialogWindow(string title, string message, string confirmText, bool destructive)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
        ConfirmButton.Style = (Style)FindResource(destructive ? "DangerButton" : "PrimaryButton");
        Loaded += (_, _) =>
        {
            WindowBackdrop.Apply(this, ThemeManager.IsDark, glass: false); // dark title bar in dark theme
            CancelButton.Focus();
        };
    }

    private void OnConfirm(object sender, RoutedEventArgs e) => DialogResult = true;
    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
