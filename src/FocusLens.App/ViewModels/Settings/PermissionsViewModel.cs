using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLens.Core.Permissions;

namespace FocusLens.App.ViewModels.Settings;

/// <summary>Shows which permissions FocusLens needs, whether each is granted, and a button to fix the ones that are not.</summary>
public sealed partial class PermissionsViewModel : ObservableObject
{
    [ObservableProperty] private string _summary = "Checking permissions...";
    [ObservableProperty] private bool _isRefreshing;

    public ObservableCollection<PermissionItemViewModel> Items { get; } = new();

    public PermissionsViewModel(IEnumerable<IPermissionCheck> checks)
    {
        foreach (var check in checks) Items.Add(new PermissionItemViewModel(check, RefreshAsync));
        _ = RefreshAsync();

        // Most fixes happen in Windows Settings, so re-check as soon as the user comes back to FocusLens.
        if (Application.Current?.MainWindow is { } window) window.Activated += (_, _) => _ = RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsRefreshing) return;
        IsRefreshing = true;
        try
        {
            await Task.WhenAll(Items.Select(i => i.RefreshAsync()));
            var missing = Items.Count(i => i.NeedsAction);
            Summary = missing == 0
                ? "All permissions are in place."
                : $"{missing} of {Items.Count} need attention.";
        }
        finally
        {
            IsRefreshing = false;
        }
    }
}
