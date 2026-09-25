using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FocusLens.App.ViewModels.Dashboard;

namespace FocusLens.App.Views;

public partial class DashboardView : UserControl
{
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromSeconds(15);
    private readonly DispatcherTimer _timer = new() { Interval = RefreshEvery };

    public DashboardView()
    {
        InitializeComponent();
        _timer.Tick += async (_, _) =>
        {
            if (DataContext is DashboardViewModel vm) await vm.RefreshIfLiveAsync();
        };
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
    }
}
