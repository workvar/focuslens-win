using System.ComponentModel;
using System.Windows;
using FocusLens.App.ViewModels;
using FocusLens.App.ViewModels.Focus;
using FocusLens.App.Views.Focus;
using FocusLens.Core.Focus;
using FocusLens.Core.Focus.Session;

namespace FocusLens.App.Services.Focus;

/// <summary>
/// The floating windows Focus Mode owns, and the tray timer:
///
///   widget   One draggable window with the pill and, when the user drifts, a drawer under it.
///            If the pill is switched off, the drawer still appears on its own.
///   overlay  Covers a blocked window until the user closes it.
///   tray     "42m left" in the tray tooltip and an "End focus session" menu item.
/// </summary>
public sealed class FocusSurfaces
{
    private readonly FocusSessionController _controller;
    private readonly FocusWidgetViewModel _widgetModel;
    private readonly FocusBlockViewModel _blockModel;
    private readonly TrayIconService _tray;
    private FocusWidgetWindow? _widget;
    private FocusBlockWindow? _overlay;
    private bool _installed;

    public FocusSurfaces(FocusSessionController controller, FocusLiveViewModel live, TrayIconService tray)
    {
        _controller = controller;
        _tray = tray;
        _widgetModel = new FocusWidgetViewModel(controller, live);
        _blockModel = new FocusBlockViewModel(controller, live);
    }

    /// <summary>Safe to call more than once. Call on the UI thread after the app's resources are loaded.</summary>
    public void Install()
    {
        if (_installed) return;
        _installed = true;

        _widgetModel.PropertyChanged += OnWidgetChanged;
        _controller.Changed += () => UiThread.Post(SyncOverlay);
        _controller.TrayTextChanged += () => UiThread.Post(SyncTray);
        _controller.Settings.Changed += () => UiThread.Post(() => { SyncTray(); ApplyEffects(); });
        _controller.Nudged += () => System.Media.SystemSounds.Exclamation.Play();
        _tray.EndFocusRequested += () => UiThread.Post(_controller.Stop);
    }

    // Widget

    private void OnWidgetChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FocusWidgetViewModel.IsVisible)) return;
        if (!_widgetModel.IsVisible)
        {
            _widget?.Hide();
            return;
        }
        var widget = _widget ??= MakeWidget();
        if (!widget.IsVisible) widget.Show();
    }

    private FocusWidgetWindow MakeWidget()
    {
        var widget = new FocusWidgetWindow { DataContext = _widgetModel };
        Place(widget);
        widget.Moved += () =>
        {
            _controller.Settings.WidgetLeft = widget.Left;
            _controller.Settings.WidgetTop = widget.Top;
            _controller.Settings.Save();
        };
        widget.SetShadow(!FocusEffects.IsReduced(_controller.Settings));
        return widget;
    }

    /// <summary>Where the user left it, if that is still on a screen; otherwise the top right of the work area.</summary>
    private void Place(Window widget)
    {
        var area = SystemParameters.WorkArea;
        var virtualScreen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var settings = _controller.Settings;
        if (settings.WidgetLeft is { } left && settings.WidgetTop is { } top
            && virtualScreen.IntersectsWith(new Rect(left, top, widget.Width, 100)))
        {
            widget.Left = left;
            widget.Top = top;
            return;
        }
        widget.Left = area.Right - widget.Width - 12;
        widget.Top = area.Top + 12;
    }

    private void ApplyEffects() => _widget?.SetShadow(!FocusEffects.IsReduced(_controller.Settings));

    // Block overlay

    private void SyncOverlay()
    {
        if (_controller.BlockTarget is not { } target)
        {
            _overlay?.Hide();
            return;
        }
        var overlay = _overlay ??= new FocusBlockWindow { DataContext = _blockModel };
        if (!overlay.IsVisible) overlay.Show();
        if (target.Bounds is { } bounds) overlay.Cover(bounds);
        else overlay.WindowState = WindowState.Maximized;
        // The widget and its warnings sit above the overlay.
        if (_widget is { IsVisible: true } widget)
        {
            widget.Topmost = false;
            widget.Topmost = true;
        }
    }

    // Tray

    private void SyncTray()
    {
        var text = _controller.TrayText;
        _tray.SetFocusItem(text is null ? null : $"End focus session ({text} left)");
        _tray.SetFocusTime(_controller.Settings.ShowTrayTimer ? text : null);
    }
}
