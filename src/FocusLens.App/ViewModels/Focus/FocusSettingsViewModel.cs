using CommunityToolkit.Mvvm.ComponentModel;
using FocusLens.Core.Focus;

namespace FocusLens.App.ViewModels.Focus;

/// <summary>
/// Focus Mode preferences. One instance backs both the Focus page ("Show my session", "Keep it
/// light") and Settings > Focus, so changing one changes the other.
/// </summary>
public sealed partial class FocusSettingsViewModel : ObservableObject
{
    private readonly FocusSettings _settings;

    [ObservableProperty] private bool _showWidget;
    [ObservableProperty] private bool _showTrayTimer;
    [ObservableProperty] private int _pollSeconds;
    [ObservableProperty] private bool _pauseWhenIdle;
    [ObservableProperty] private FocusVisualEffects _visualEffects;
    [ObservableProperty] private string _model;
    [ObservableProperty] private int _patienceSeconds;
    [ObservableProperty] private int _countdownSeconds;
    [ObservableProperty] private string _allowlist;

    public FocusSettingsViewModel(FocusSettings settings)
    {
        _settings = settings;
        _showWidget = settings.ShowWidget;
        _showTrayTimer = settings.ShowTrayTimer;
        _pollSeconds = settings.PollInterval;
        _pauseWhenIdle = settings.PauseWhenIdle;
        _visualEffects = settings.VisualEffects;
        _model = settings.Model;
        _patienceSeconds = settings.Patience;
        _countdownSeconds = settings.Countdown;
        _allowlist = settings.Allowlist;
    }

    public IReadOnlyList<PollChoice> PollChoices { get; } =
        FocusSettings.PollChoices.Select(s => new PollChoice(s, $"{s} seconds")).ToList();

    public IReadOnlyList<VisualEffectsChoice> VisualEffectsChoices { get; } = new VisualEffectsChoice[]
    {
        new(FocusVisualEffects.Automatic, "Automatic"),
        new(FocusVisualEffects.Full, "Full"),
        new(FocusVisualEffects.Minimal, "Minimal"),
    };

    public string PatienceText => $"{PatienceSeconds} seconds";
    public string CountdownText => $"{CountdownSeconds} seconds";

    partial void OnShowWidgetChanged(bool value) => Save(s => s.ShowWidget = value);
    partial void OnShowTrayTimerChanged(bool value) => Save(s => s.ShowTrayTimer = value);
    partial void OnPollSecondsChanged(int value) => Save(s => s.PollSeconds = value);
    partial void OnPauseWhenIdleChanged(bool value) => Save(s => s.PauseWhenIdle = value);
    partial void OnVisualEffectsChanged(FocusVisualEffects value) => Save(s => s.VisualEffects = value);
    partial void OnModelChanged(string value) => Save(s => s.Model = value.Trim());
    partial void OnAllowlistChanged(string value) => Save(s => s.Allowlist = value);

    partial void OnPatienceSecondsChanged(int value)
    {
        Save(s => s.PatienceSeconds = Math.Clamp(value, 15, 180));
        OnPropertyChanged(nameof(PatienceText));
    }

    partial void OnCountdownSecondsChanged(int value)
    {
        Save(s => s.CountdownSeconds = Math.Clamp(value, 3, 15));
        OnPropertyChanged(nameof(CountdownText));
    }

    private void Save(Action<FocusSettings> change)
    {
        change(_settings);
        _settings.Save();
    }
}

/// <remarks>ToString returns the label: the themed ComboBox shows the selected item as text.</remarks>
public sealed record PollChoice(int Seconds, string Label)
{
    public override string ToString() => Label;
}

/// <remarks>ToString returns the label: the themed ComboBox shows the selected item as text.</remarks>
public sealed record VisualEffectsChoice(FocusVisualEffects Value, string Label)
{
    public override string ToString() => Label;
}
