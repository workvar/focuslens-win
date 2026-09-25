using System.Collections.Specialized;
using System.Windows;
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
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ChatViewModel vm) return;

        vm.Messages.CollectionChanged += OnMessagesChanged;
        vm.NewChatStarted += FocusInput;
        if (vm.IsEmpty) FocusInput();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ChatViewModel vm) return;

        vm.Messages.CollectionChanged -= OnMessagesChanged;
        vm.NewChatStarted -= FocusInput;
    }

    private void FocusInput()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!IsVisible) return;
            Input.Focus();
            Input.CaretIndex = Input.Text.Length;
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private void CopyMessage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ChatMessageViewModel message } && !string.IsNullOrEmpty(message.Content))
            Clipboard.SetText(message.Content);
    }

    private void EditMessage_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ChatViewModel viewModel || sender is not FrameworkElement { DataContext: ChatMessageViewModel message })
            return;

        viewModel.InputText = message.Content;
        FocusInput();
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
