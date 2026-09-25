using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLens.App.Services.Dialogs;
using FocusLens.Core.Setup;

namespace FocusLens.App.ViewModels.Settings;

/// <summary>
/// One installable component (Ollama, a model, Chroma): its status, a confirm-then-install action with progress,
/// and a link for installing by hand. Nothing is downloaded until the user says yes in the confirmation.
/// </summary>
public sealed partial class ToolSetupItemViewModel : ObservableObject
{
    private readonly Func<Task<(bool Ready, string Detail)>> _check;
    private readonly Func<IProgress<SetupProgress>, CancellationToken, Task> _install;
    private readonly Func<(string Title, string Message)> _confirm;
    private readonly Func<Task>? _afterChange;
    private CancellationTokenSource? _cts;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanInstall), nameof(StatusLabel))] private bool _isReady;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanInstall), nameof(StatusLabel))] private bool _isChecked;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanInstall))] private bool _isBusy;
    [ObservableProperty] private string _detail = "Checking...";
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool _isIndeterminate = true;
    [ObservableProperty] private string? _error;

    public string Name { get; }
    public string Description { get; }
    public string InstallLabel { get; }
    public string? ManualUrl { get; }

    public bool CanInstall => IsChecked && !IsReady && !IsBusy;
    public string StatusLabel => !IsChecked ? "Checking" : IsReady ? "Ready" : "Not set up";

    public ToolSetupItemViewModel(
        string name, string description, string installLabel, string? manualUrl,
        Func<Task<(bool Ready, string Detail)>> check,
        Func<IProgress<SetupProgress>, CancellationToken, Task> install,
        Func<(string Title, string Message)> confirm,
        Func<Task>? afterChange = null)
    {
        Name = name;
        Description = description;
        InstallLabel = installLabel;
        ManualUrl = manualUrl;
        _check = check;
        _install = install;
        _confirm = confirm;
        _afterChange = afterChange;
    }

    [RelayCommand]
    public async Task RecheckAsync()
    {
        try
        {
            var (ready, detail) = await _check();
            IsReady = ready;
            Detail = detail;
        }
        catch (Exception ex)
        {
            IsReady = false;
            Detail = $"Could not check: {ex.Message}";
        }
        IsChecked = true;
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        var (title, message) = _confirm();
        if (!ConfirmDialog.Ask(title, message, InstallLabel, destructive: false)) return;

        Error = null;
        IsBusy = true;
        IsIndeterminate = true;
        Message = "Starting...";
        _cts = new CancellationTokenSource();
        var progress = new Progress<SetupProgress>(p =>
        {
            Message = p.Message;
            IsIndeterminate = p.Fraction < 0;
            if (p.Fraction >= 0) Progress = p.Fraction;
        });

        try
        {
            await _install(progress, _cts.Token);
            Message = "Done.";
        }
        catch (OperationCanceledException)
        {
            Message = "";
            Error = "Cancelled.";
        }
        catch (SetupException ex)
        {
            Message = "";
            Error = ex.Message;
        }
        catch (Exception ex)
        {
            Message = "";
            Error = $"Setup failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cts.Dispose();
            _cts = null;
        }

        await RecheckAsync();
        if (_afterChange is not null) await _afterChange();
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand]
    private void OpenManual()
    {
        if (ManualUrl is null) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ManualUrl) { UseShellExecute = true }); }
        catch { /* no default browser configured */ }
    }
}
