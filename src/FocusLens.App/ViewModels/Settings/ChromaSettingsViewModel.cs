using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLens.App.Services.Dialogs;
using FocusLens.Core.Chroma;

namespace FocusLens.App.ViewModels.Settings;

/// <summary>The Memory tab: switch semantic search on or off, see what is indexed, rebuild or delete it.</summary>
public sealed partial class ChromaSettingsViewModel : ObservableObject
{
    private readonly ChromaIndexService _index;

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isInstalled = true;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _progress = "";
    [ObservableProperty] private string _lastUpdated = "Never";
    [ObservableProperty] private int _activityCount;
    [ObservableProperty] private int _screenTextCount;
    [ObservableProperty] private int _summaryCount;
    [ObservableProperty] private int _messageCount;

    public ChromaSettingsViewModel(ChromaIndexService index)
    {
        _index = index;
        _index.Changed += () => UiThread.Post(Sync);
        Sync();
    }

    public bool Enabled
    {
        get => _index.Enabled;
        set { _index.Enabled = value; OnPropertyChanged(); }
    }

    public Task RefreshAsync() => _index.RefreshStatusAsync();

    [RelayCommand]
    private Task UpdateNowAsync() => _index.ReindexAsync(full: false);

    [RelayCommand]
    private Task RebuildAsync() => _index.ReindexAsync(full: true);

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var confirmed = ConfirmDialog.Ask(
            "Delete the search index?",
            "Your activity data is not touched. The index can be rebuilt at any time.",
            "Delete index");
        if (confirmed) await _index.ClearAsync();
    }

    private void Sync()
    {
        IsBusy = _index.State == ChromaIndexState.Indexing;
        IsInstalled = _index.State != ChromaIndexState.NotInstalled;
        HasError = _index.State == ChromaIndexState.Failed;
        Progress = IsBusy ? _index.Message : "";
        StatusText = _index.State switch
        {
            ChromaIndexState.NotInstalled => "Chroma is not installed. Set it up in Local tools.",
            ChromaIndexState.Indexing => "Indexing...",
            ChromaIndexState.Failed => _index.Message,
            _ => _index.Counts.Total == 0 ? "Ready. Nothing indexed yet." : "Ready.",
        };
        LastUpdated = _index.LastIndexedUtc is { } utc ? utc.ToLocalTime().ToString("g") : "Never";
        ActivityCount = _index.Counts.Of("activity");
        ScreenTextCount = _index.Counts.Of("screenshots");
        SummaryCount = _index.Counts.Of("summaries");
        MessageCount = _index.Counts.Of("conversations");
        OnPropertyChanged(nameof(Enabled));
    }
}
