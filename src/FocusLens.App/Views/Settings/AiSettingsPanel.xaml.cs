using System.Windows;
using System.Windows.Controls;
using FocusLens.App.ViewModels.Settings;

namespace FocusLens.App.Views.Settings;

public partial class AiSettingsPanel : UserControl
{
    public AiSettingsPanel() => InitializeComponent();

    // PasswordBox content cannot be data-bound, so keys are forwarded to the view model here.
    private void OnClaudeKeyChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is AiSettingsViewModel vm) vm.SetClaudeKey(ClaudeKey.Password);
    }

    private void OnOpenAiKeyChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is AiSettingsViewModel vm) vm.SetOpenAiKey(OpenAiKey.Password);
    }
}
