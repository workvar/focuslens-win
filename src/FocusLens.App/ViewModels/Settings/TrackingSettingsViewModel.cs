using CommunityToolkit.Mvvm.ComponentModel;
using FocusLens.Core.Settings;

namespace FocusLens.App.ViewModels.Settings;

public sealed partial class TrackingToggleViewModel : ObservableObject
{
    private readonly TrackingSettingsViewModel _owner;
    public TrackingCatalogEntry Entry { get; }

    [ObservableProperty] private bool _isOn;
    [ObservableProperty] private bool _isAvailable = true;

    public string Title => Entry.Title;
    public string Detail => Entry.Detail;

    public TrackingToggleViewModel(TrackingCatalogEntry entry, bool isOn, TrackingSettingsViewModel owner)
    {
        Entry = entry;
        _owner = owner;
        _isOn = isOn;
    }

    partial void OnIsOnChanged(bool value) => _owner.Changed(this);
}

public sealed record TrackingSectionViewModel(string Title, string Footer, IReadOnlyList<TrackingToggleViewModel> Toggles);

/// <summary>The data-collection switches. Each change is saved immediately; the agent re-reads within seconds.</summary>
public sealed class TrackingSettingsViewModel
{
    private readonly TrackingSettings _settings = TrackingSettings.Load();
    private readonly List<TrackingToggleViewModel> _all = new();
    private bool _updating;

    public IReadOnlyList<TrackingSectionViewModel> Sections { get; }

    public TrackingSettingsViewModel()
    {
        Sections = TrackingCatalog.Sections.Select(section =>
        {
            var toggles = section.Entries.Select(e => new TrackingToggleViewModel(e, _settings.IsSet(e.Item), this)).ToList();
            _all.AddRange(toggles);
            return new TrackingSectionViewModel(section.Title, section.Footer, toggles);
        }).ToList();
        RefreshAvailability();
    }

    public void Changed(TrackingToggleViewModel toggle)
    {
        if (_updating) return;
        _settings.Set(toggle.Entry.Item, toggle.IsOn);
        _settings.Save();
        RefreshAvailability();
    }

    /// <summary>Dependent switches are greyed out while the switch they need is off.</summary>
    private void RefreshAvailability()
    {
        _updating = true;
        foreach (var toggle in _all)
            toggle.IsAvailable = toggle.Entry.Item.Requires() is not { } parent || _settings.IsOn(parent);
        _updating = false;
    }
}
