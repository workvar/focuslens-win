using CommunityToolkit.Mvvm.ComponentModel;
using FocusLens.Core.Health;

namespace FocusLens.App.ViewModels.Status;

/// <summary>One row of the status list: a service name, its state and a one-line explanation.</summary>
public sealed partial class ServiceStatusItem : ObservableObject
{
    [ObservableProperty]
    private ServiceState _state = ServiceState.Checking;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Tooltip))]
    private string _detail = "Checking...";

    public ServiceStatusItem(string name) => Name = name;

    public string Name { get; }
    public string Tooltip => $"{Name}: {Detail}";

    public void Apply(ServiceHealth health)
    {
        State = health.State;
        Detail = health.Detail;
    }
}
