using System.Windows.Controls;
using FocusLens.App.ViewModels.Settings;

namespace FocusLens.App.Views.Settings;

public partial class ChromaSettingsPanel : UserControl
{
    public ChromaSettingsPanel()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is ChromaSettingsViewModel vm) await vm.RefreshAsync();
        };
    }
}
