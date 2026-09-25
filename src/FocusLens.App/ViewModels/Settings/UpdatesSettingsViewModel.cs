using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLens.App.Services;

namespace FocusLens.App.ViewModels.Settings;

/// <summary>Installed version, the latest GitHub release, and the download and restart buttons.</summary>
public sealed partial class UpdatesSettingsViewModel : ObservableObject
{
    private readonly UpdateService _updates;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Headline), nameof(ShowCheck), nameof(ShowDownload), nameof(ShowRestart),
        nameof(IsDownloading), nameof(HasNotes), nameof(HasError))]
    private UpdateState _state;

    /// <summary>Raised when the user chooses to restart into the downloaded update.</summary>
    public event Action? RestartRequested;

    public UpdatesSettingsViewModel(UpdateService updates)
    {
        _updates = updates;
        _state = updates.State;
        updates.StateChanged += next => UiThread.Post(() => State = next);
    }

    public string CurrentVersion => _updates.CurrentVersion;
    public bool IsInstalled => _updates.IsInstalled;

    public string Headline => !IsInstalled
        ? "Updates are only available in the installed app."
        : State.Status switch
        {
            UpdateStatus.Checking => "Checking for updates...",
            UpdateStatus.UpToDate => "You are up to date.",
            UpdateStatus.Available => $"FocusLens {State.Version} is available.",
            UpdateStatus.Downloading => $"Downloading {State.Version}... {State.Progress}%",
            UpdateStatus.Ready => $"{State.Version} is downloaded. Restart to finish updating.",
            UpdateStatus.Failed => State.Error ?? "Could not check for updates.",
            _ => "FocusLens checks GitHub Releases every few hours.",
        };

    public bool ShowCheck => IsInstalled && State.Status is not (UpdateStatus.Available or UpdateStatus.Downloading or UpdateStatus.Ready);
    public bool ShowDownload => IsInstalled && State.Status == UpdateStatus.Available;
    public bool ShowRestart => IsInstalled && State.Status == UpdateStatus.Ready;
    public bool IsDownloading => State.Status == UpdateStatus.Downloading;
    public bool HasNotes => !string.IsNullOrWhiteSpace(State.Notes);
    public bool HasError => State.Status == UpdateStatus.Available && State.Error is not null;

    [RelayCommand]
    private Task CheckAsync() => _updates.CheckAsync();

    [RelayCommand]
    private Task DownloadAsync() => _updates.DownloadAsync();

    [RelayCommand]
    private void Restart() => RestartRequested?.Invoke();
}
