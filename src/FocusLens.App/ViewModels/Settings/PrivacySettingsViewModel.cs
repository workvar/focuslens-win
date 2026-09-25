using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLens.Core.Privacy;

namespace FocusLens.App.ViewModels.Settings;

public sealed partial class PrivacyCategoryViewModel : ObservableObject
{
    private readonly PrivacySettingsViewModel _owner;
    public PrivacyCategory Model { get; }

    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private string _appsText;
    [ObservableProperty] private string _domainsText;
    [ObservableProperty] private string _keywordsText;
    [ObservableProperty] private bool _isExpanded;

    public string Name => Model.Name;
    public bool IsSystem => Model.IsSystem;
    public string Summary =>
        $"{Model.AppIds.Count} apps, {Model.Domains.Count} sites, {Model.Keywords.Count} keywords";

    public PrivacyCategoryViewModel(PrivacyCategory model, PrivacySettingsViewModel owner)
    {
        Model = model;
        _owner = owner;
        _isEnabled = model.IsEnabled;
        _appsText = string.Join("\n", model.AppIds);
        _domainsText = string.Join("\n", model.Domains);
        _keywordsText = string.Join("\n", model.Keywords);
    }

    partial void OnIsEnabledChanged(bool value) { Model.IsEnabled = value; _owner.Save(); }
    partial void OnAppsTextChanged(string value) { Model.AppIds = Split(value); Refresh(); }
    partial void OnDomainsTextChanged(string value) { Model.Domains = Split(value); Refresh(); }
    partial void OnKeywordsTextChanged(string value) { Model.Keywords = Split(value); Refresh(); }

    private void Refresh()
    {
        OnPropertyChanged(nameof(Summary));
        _owner.Save();
    }

    private static List<string> Split(string text) =>
        text.Split(new[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}

/// <summary>Sensitive-category editor. Enabled categories are excluded from recording entirely.</summary>
public sealed partial class PrivacySettingsViewModel : ObservableObject
{
    private readonly PrivacyConfig _config = PrivacyConfig.Load();

    [ObservableProperty] private string _newCategoryName = "";

    public ObservableCollection<PrivacyCategoryViewModel> Categories { get; } = new();

    public PrivacySettingsViewModel()
    {
        foreach (var category in _config.Categories) Categories.Add(new PrivacyCategoryViewModel(category, this));
    }

    public void Save() => _config.Save();

    [RelayCommand]
    private void AddCategory()
    {
        var name = NewCategoryName.Trim();
        if (name.Length == 0) return;
        var category = new PrivacyCategory
        {
            Id = "custom-" + Guid.NewGuid().ToString("N")[..8], Name = name, Icon = "shield", ColorHex = "#64748B",
            IsSystem = false, IsEnabled = true,
        };
        _config.Categories.Add(category);
        Categories.Add(new PrivacyCategoryViewModel(category, this) { IsExpanded = true });
        NewCategoryName = "";
        Save();
    }

    [RelayCommand]
    private void RemoveCategory(PrivacyCategoryViewModel category)
    {
        if (category.IsSystem) return;
        _config.Categories.Remove(category.Model);
        Categories.Remove(category);
        Save();
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        var defaults = PrivacyDefaults.Create();
        _config.Categories.Clear();
        _config.Categories.AddRange(defaults.Categories);
        Categories.Clear();
        foreach (var category in _config.Categories) Categories.Add(new PrivacyCategoryViewModel(category, this));
        Save();
    }
}
