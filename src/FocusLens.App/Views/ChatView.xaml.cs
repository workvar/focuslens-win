using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Input;
using FocusLens.App.ViewModels.Chat;

namespace FocusLens.App.Views;

public partial class ChatView : UserControl
{
    public ChatView()
    {
        InitializeComponent();
        // The view model outlives the view, so subscribe only while the view is on screen.
        Loaded += (_, _) => { if (DataContext is ChatViewModel vm) vm.Messages.CollectionChanged += OnMessagesChanged; };
        Unloaded += (_, _) => { if (DataContext is ChatViewModel vm) vm.Messages.CollectionChanged -= OnMessagesChanged; };
    }

    /// <summary>Enter sends; the command's own CanExecute decides whether that is allowed right now.</summary>
    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not ChatViewModel vm) return;
        if (vm.SendCommand.CanExecute(null)) vm.SendCommand.Execute(null);
        e.Handled = true;
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.BeginInvoke(() => Scroller.ScrollToEnd());
}
