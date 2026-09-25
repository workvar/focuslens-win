using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLens.Core.Health;

namespace FocusLens.App.ViewModels.Status;

/// <summary>Live connection status of the agent, SQLite, Chroma DB and the LLM, shared by the sidebar and Settings.</summary>
public sealed partial class StatusViewModel : ObservableObject
{
    private readonly HealthMonitor _monitor;

    [ObservableProperty] private string _summary = "Checking services...";
    [ObservableProperty] private bool _isRefreshing;

    public ObservableCollection<ServiceStatusItem> Items { get; } = new();

    public StatusViewModel(HealthMonitor monitor)
    {
        _monitor = monitor;
        foreach (var name in monitor.Names) Items.Add(new ServiceStatusItem(name));
        monitor.Updated += health => UiThread.Post(() => Apply(health));
    }

    /// <summary>Cheap to call often: each probe skips itself until its own interval has passed.</summary>
    public Task RefreshAsync(bool force = false) => _monitor.RefreshAsync(force);

    [RelayCommand]
    private async Task RecheckAsync()
    {
        IsRefreshing = true;
        try { await _monitor.RefreshAsync(force: true); }
        finally { IsRefreshing = false; }
    }

    private void Apply(ServiceHealth health)
    {
        Items.FirstOrDefault(i => i.Name == health.Name)?.Apply(health);

        var connected = Items.Count(i => i.State == ServiceState.Connected);
        Summary = connected == Items.Count
            ? "All services connected"
            : $"{connected} of {Items.Count} services connected";
    }
}
