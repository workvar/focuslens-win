using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLens.Core.Focus.Session;

namespace FocusLens.App.ViewModels.Focus;

/// <summary>
/// The overlay laid over a blocked window. It stays until the user closes what it covers or
/// leaves it. There is always a way out: ending the session.
/// </summary>
public sealed partial class FocusBlockViewModel : ObservableObject
{
    private readonly FocusSessionController _controller;

    [ObservableProperty] private string _message = "";
    [ObservableProperty] private string _closeLabel = "Close this window";

    public FocusBlockViewModel(FocusSessionController controller, FocusLiveViewModel live)
    {
        _controller = controller;
        Live = live;
        controller.Changed += () => UiThread.Post(Sync);
        Sync();
    }

    public FocusLiveViewModel Live { get; }

    private void Sync()
    {
        if (_controller.BlockTarget is not { } target || _controller.Session is not { } session) return;
        Message = $"You are focusing on {session.Goal}. {target.Label} stays covered until you close it.";
        CloseLabel = $"Close this {target.Noun}";
    }

    [RelayCommand]
    private void CloseTarget() => _controller.CloseBlockedTarget();

    [RelayCommand]
    private void EndSession() => _controller.Stop();
}
