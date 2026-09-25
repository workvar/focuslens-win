using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLens.Core.Focus.Session;

namespace FocusLens.App.ViewModels.Focus;

/// <summary>
/// The floating widget: the pill (score, goal, time left, patience bar) and, when the user
/// drifts, a drawer under it with the nudge or the countdown. If the pill is switched off, the
/// drawer still appears on its own, because the warning is what makes acting fair.
/// </summary>
public sealed partial class FocusWidgetViewModel : ObservableObject
{
    private readonly FocusSessionController _controller;

    [ObservableProperty] private FocusAlertContent? _drawer;
    [ObservableProperty] private bool _showsPill;
    [ObservableProperty] private bool _showsDrawer;
    [ObservableProperty] private bool _isVisible;

    public FocusWidgetViewModel(FocusSessionController controller, FocusLiveViewModel live)
    {
        _controller = controller;
        Live = live;
        controller.Changed += () => UiThread.Post(Sync);
        controller.Settings.Changed += () => UiThread.Post(Sync);
        Sync();
    }

    public FocusLiveViewModel Live { get; }

    private void Sync()
    {
        var session = _controller.Session;
        Drawer = FocusAlertContent.Make(_controller.Alert, _controller.Distraction, session?.Goal, session?.Enforcement);
        ShowsPill = session is not null && _controller.Settings.ShowWidget;
        ShowsDrawer = Drawer is not null;
        IsVisible = ShowsPill || ShowsDrawer;
    }

    [RelayCommand]
    private void Stop() => _controller.Stop();

    [RelayCommand]
    private void Snooze() => _controller.SnoozeCountdown();
}
