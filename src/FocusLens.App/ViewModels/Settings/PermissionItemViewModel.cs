using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLens.Core.Permissions;

namespace FocusLens.App.ViewModels.Settings;

/// <summary>One row on the Permissions page.</summary>
public sealed partial class PermissionItemViewModel : ObservableObject
{
    private readonly IPermissionCheck _check;
    private readonly Func<Task> _afterGrant;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGranted), nameof(NeedsAction), nameof(StatusLabel), nameof(CanGrant))]
    private PermissionState? _state;
    [ObservableProperty] private string _detail = "Checking...";

    public PermissionItemViewModel(IPermissionCheck check, Func<Task> afterGrant)
    {
        _check = check;
        _afterGrant = afterGrant;
    }

    public string Name => _check.Name;
    public string Purpose => _check.Purpose;
    public string GrantLabel => _check.GrantLabel;

    public bool IsGranted => State == PermissionState.Granted;
    public bool NeedsAction => State is PermissionState.Denied or PermissionState.Unavailable;
    /// <summary>Shown as text as well as colour, so the state never depends on colour alone.</summary>
    public string StatusLabel => State switch
    {
        PermissionState.Granted => "Granted",
        PermissionState.Denied => "Not granted",
        PermissionState.Unavailable => "Unavailable",
        _ => "Checking",
    };
    public bool CanGrant => NeedsAction;

    public async Task RefreshAsync()
    {
        try
        {
            var status = await _check.CheckAsync();
            State = status.State;
            Detail = status.Detail;
        }
        catch (Exception ex)
        {
            State = PermissionState.Unavailable;
            Detail = $"Could not check: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task GrantAsync()
    {
        await _check.GrantAsync();
        await _afterGrant();
    }
}
