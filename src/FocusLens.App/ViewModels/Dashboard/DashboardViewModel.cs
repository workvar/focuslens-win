using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLens.Core.Ai.Chat;
using FocusLens.Core.Models;
using FocusLens.Core.Repositories;
using FocusLens.Core.Summary;

namespace FocusLens.App.ViewModels.Dashboard;

/// <summary>Daily overview: focus score, category split, top apps, hourly rhythm and 7-day trend.</summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    private const double ChartHeight = 110;
    private readonly ActivityRepository _repository;

    [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _hasData;
    [ObservableProperty] private string _updatedText = "";

    [ObservableProperty] private string _focusScoreText = "0";
    [ObservableProperty] private string _focusLabel = "No data";
    [ObservableProperty] private string _deltaText = "";

    [ObservableProperty] private ChartPayload? _categoryChart;
    [ObservableProperty] private ChartPayload? _trendChart;

    public ObservableCollection<StatCard> Stats { get; } = new();
    public ObservableCollection<LegendRow> Legend { get; } = new();
    public ObservableCollection<AppRow> TopApps { get; } = new();
    public ObservableCollection<HourBar> Hours { get; } = new();

    public bool IsToday => SelectedDate.Date == DateTime.Today;
    public string DateLabel => IsToday ? "Today" : SelectedDate.ToString("dddd, MMM d");

    public DashboardViewModel(ActivityRepository repository) => _repository = repository;

    partial void OnSelectedDateChanged(DateTime value)
    {
        OnPropertyChanged(nameof(IsToday));
        OnPropertyChanged(nameof(DateLabel));
    }

    [RelayCommand]
    private Task PreviousDay() { SelectedDate = SelectedDate.AddDays(-1); return LoadAsync(); }

    [RelayCommand]
    private Task NextDay()
    {
        if (IsToday) return Task.CompletedTask;
        SelectedDate = SelectedDate.AddDays(1);
        return LoadAsync();
    }

    [RelayCommand]
    private Task GoToToday() { SelectedDate = DateTime.Today; return LoadAsync(); }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await _repository.RebuildSummaryIfNeededAsync(SelectedDate);
            var summary = await _repository.FetchDailySummaryAsync(SelectedDate);
            var hourly = await _repository.FetchHourlyBreakdownAsync(SelectedDate);
            var previous = await _repository.FetchDailySummaryAsync(SelectedDate.AddDays(-1));
            var trend = await _repository.FetchSummariesAsync(SelectedDate.AddDays(-6), SelectedDate);

            Apply(summary, previous);
            ApplyHours(hourly);
            ApplyTrend(trend);
            UpdatedText = $"Updated {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Timer entry point: keeps today's numbers current without stacking loads or touching past days.</summary>
    public Task RefreshIfLiveAsync() => IsToday && !IsLoading ? LoadAsync() : Task.CompletedTask;

    private void Apply(DailySummary? summary, DailySummary? previous)
    {
        HasData = summary is not null;
        Stats.Clear();
        Legend.Clear();
        TopApps.Clear();

        if (summary is null)
        {
            FocusScoreText = "0";
            FocusLabel = "No data";
            DeltaText = "";
            CategoryChart = null;
            return;
        }

        FocusScoreText = summary.FocusScore.ToString("0");
        FocusLabel = summary.FocusLabel;
        DeltaText = previous is null
            ? ""
            : $"{summary.FocusScore - previous.FocusScore:+0;-0;0} pts vs the day before";

        Stats.Add(new StatCard("Active time", Format(summary.TotalActiveS), "Time with input"));
        Stats.Add(new StatCard("Idle time", Format(summary.TotalIdleS), "No input for 60 s"));
        Stats.Add(new StatCard("Deep Work", Format(summary.CategoryTotals.FirstOrDefault(c => c.Name == FocusScore.DeepWorkCategory)?.Seconds ?? 0), "Counts toward focus"));

        var total = Math.Max(1, summary.CategoryTotals.Sum(c => c.Seconds));
        foreach (var category in summary.CategoryTotals)
            Legend.Add(new LegendRow(category.Name, Format(category.Seconds), $"{category.Seconds * 100.0 / total:0}%", BrushFactory.FromHex(category.ColorHex)));

        CategoryChart = new ChartPayload(ChartType.Pie, summary.CategoryTotals
            .Select(c => new ChartDataPoint(c.Id, c.Name, c.Seconds, c.ColorHex)).ToList());

        var max = Math.Max(1, summary.TopApps.Take(8).Max(a => a.Seconds));
        foreach (var app in summary.TopApps.Take(8))
            TopApps.Add(new AppRow(app.Name, Format(app.Seconds), app.Seconds / (double)max));
    }

    private void ApplyHours(IReadOnlyList<HourlyBucket> hourly)
    {
        Hours.Clear();
        var max = Math.Max(60, hourly.Max(h => h.TotalSeconds));
        foreach (var bucket in hourly)
        {
            var segments = bucket.Segments
                .Select(s => new HourSegment(ChartHeight * s.Seconds / max, BrushFactory.FromHex(s.ColorHex))).ToList();
            var tooltip = $"{bucket.Hour:00}:00, {Format(bucket.TotalSeconds)} active";
            Hours.Add(new HourBar(bucket.Hour % 3 == 0 ? bucket.Hour.ToString("00") : "", tooltip, segments));
        }
    }

    private void ApplyTrend(IReadOnlyList<DailySummary> trend) =>
        TrendChart = trend.Count < 2
            ? null
            : new ChartPayload(ChartType.Line, trend
                .Select(t => new ChartDataPoint(t.Date, DateTime.Parse(t.Date).ToString("ddd"), t.FocusScore, "#1A5D38")).ToList());

    private static string Format(int seconds) =>
        seconds >= 3600 ? $"{seconds / 3600}h {seconds % 3600 / 60}m" : $"{seconds / 60}m";
}
