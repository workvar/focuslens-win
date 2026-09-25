using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FocusLens.App.Services.Focus;
using FocusLens.Core.Focus;
using FocusLens.Core.Focus.Session;

namespace FocusLens.App.ViewModels.Focus;

/// <summary>
/// The running session as the page, the widget and the overlay show it: goal, time left, live
/// score and patience bar. The clock ticks every second, or once a minute with reduced effects.
/// </summary>
public sealed partial class FocusLiveViewModel : ObservableObject
{
    private readonly FocusSessionController _controller;
    private readonly DispatcherTimer _clock = new(DispatcherPriority.Background);

    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private string _goal = "";
    [ObservableProperty] private string _remainingText = "";
    [ObservableProperty] private int _score = 100;
    [ObservableProperty] private double _meterLevel = 1;
    [ObservableProperty] private string _meterCaption = "";
    [ObservableProperty] private bool _isReduced;

    public FocusLiveViewModel(FocusSessionController controller)
    {
        _controller = controller;
        _clock.Tick += (_, _) => UpdateRemaining();
        controller.LiveChanged += () => UiThread.Post(() =>
        {
            Score = controller.LiveScore;
            MeterLevel = controller.MeterLevel;
        });
        controller.Changed += () => UiThread.Post(Sync);
        controller.Settings.Changed += () => UiThread.Post(ApplyEffects);
        ApplyEffects();
        Sync();
    }

    private void Sync()
    {
        var session = _controller.Session;
        IsActive = session is not null;
        Goal = session?.Goal ?? "";
        MeterCaption = session is null ? "" : $"Patience bar. When it empties, the tab or window is {session.Enforcement.Fate()}.";
        UpdateRemaining();
        if (IsActive) _clock.Start();
        else _clock.Stop();
    }

    private void ApplyEffects()
    {
        IsReduced = FocusEffects.IsReduced(_controller.Settings);
        _clock.Interval = TimeSpan.FromSeconds(IsReduced ? 60 : 1);
        UpdateRemaining();
    }

    private void UpdateRemaining()
    {
        if (_controller.Session is not { } session)
        {
            RemainingText = "";
            return;
        }
        var left = session.Remaining(DateTime.UtcNow);
        RemainingText = IsReduced ? FocusFormat.RemainingMinutes(left) : FocusFormat.Remaining(left);
    }
}
