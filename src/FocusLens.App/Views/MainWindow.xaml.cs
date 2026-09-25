using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FocusLens.App.ViewModels;
using FocusLens.Core.Models;

namespace FocusLens.App.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void OnConversationRenameLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: Conversation conversation } && DataContext is MainViewModel viewModel)
            _ = viewModel.CommitRenameConversationAsync(conversation);
    }

    private void OnConversationRenameKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox { DataContext: Conversation conversation } || DataContext is not MainViewModel viewModel)
            return;

        e.Handled = true;
        _ = viewModel.CommitRenameConversationAsync(conversation);
        Keyboard.ClearFocus();
    }

    private void OnConversationRenameIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.IsVisible)
        {
            Dispatcher.InvokeAsync(() =>
            {
                textBox.Focus();
                textBox.SelectAll();
            }, System.Windows.Threading.DispatcherPriority.Input);
        }
    }
}
