using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLens.App.Services;
using FocusLens.App.ViewModels.Chat;
using FocusLens.App.ViewModels.Dashboard;
using FocusLens.App.ViewModels.Meetings;
using FocusLens.App.ViewModels.Settings;
using FocusLens.Core.Models;

namespace FocusLens.App.ViewModels;

public enum NavPage
{
    Dashboard,
    Chat,
    Meetings,
    Settings,
}

/// <summary>Shell state: navigation, sidebar, conversation list, agent status and the meeting strip.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(5) };

    [ObservableProperty] private ObservableObject? _currentPage;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDashboardActive), nameof(IsChatActive), nameof(IsMeetingsActive), nameof(IsSettingsActive))]
    private NavPage _currentNav = NavPage.Dashboard;
    [ObservableProperty] private bool _isOnboarding;
    [ObservableProperty] private bool _isSidebarCollapsed;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private string _agentStatusText = "";
    [ObservableProperty] private Conversation? _selectedConversation;
    [ObservableProperty] private string? _restrictionNotice;

    public bool IsDashboardActive => CurrentNav == NavPage.Dashboard;
    public bool IsChatActive => CurrentNav == NavPage.Chat;
    public bool IsMeetingsActive => CurrentNav == NavPage.Meetings;
    public bool IsSettingsActive => CurrentNav == NavPage.Settings;

    public DashboardViewModel Dashboard { get; }
    public ChatViewModel Chat { get; }
    public MeetingsViewModel Meetings { get; }
    public SettingsViewModel Settings { get; }
    public MeetingBannerViewModel Banner { get; }
    public OnboardingViewModel Onboarding { get; }
    public ObservableCollection<Conversation> Conversations { get; } = new();

    public MainViewModel(AppServices services)
    {
        _services = services;

        Dashboard = new DashboardViewModel(services.Activity);
        Chat = new ChatViewModel(services.Conversations, services.Query);
        Meetings = new MeetingsViewModel(services.Meetings, services.MeetingSummarizer, services.MeetingSession);
        Banner = new MeetingBannerViewModel(services.MeetingSession, services.MeetingDetection);
        Settings = new SettingsViewModel(
            new GeneralSettingsViewModel(services.Settings, services.Agent, services.Auth,
                () => ThemeManager.Apply(services.Settings.Appearance)),
            new TrackingSettingsViewModel(),
            new PrivacySettingsViewModel(),
            new AiSettingsViewModel(services.Ai, services.Secrets, services.AiClient),
            new MeetingSettingsViewModel(services.Ai));
        Onboarding = new OnboardingViewModel(services, CompleteOnboarding);

        IsOnboarding = !services.Settings.OnboardingCompleted;
        IsSidebarCollapsed = services.Settings.SidebarCollapsed;
        _currentPage = Dashboard;

        services.Conversations.Changed += () => UiThread.Post(() => _ = ReloadConversationsAsync());
        _statusTimer.Tick += (_, _) => RefreshStatus();
        _statusTimer.Start();
        RefreshStatus();
    }

    public async Task InitializeAsync()
    {
        await ReloadConversationsAsync();
        await Meetings.RefreshAsync();
        await Dashboard.LoadAsync();
    }

    private void CompleteOnboarding()
    {
        _services.Settings.OnboardingCompleted = true;
        _services.Settings.Save();
        IsOnboarding = false;
        _services.Agent.SetAutoStart(_services.Settings.StartWithWindows);
        _services.Agent.Start();
        _ = Dashboard.LoadAsync();
    }

    partial void OnIsSidebarCollapsedChanged(bool value)
    {
        _services.Settings.SidebarCollapsed = value;
        _services.Settings.Save();
    }

    [RelayCommand]
    private async Task NavigateAsync(NavPage page)
    {
        CurrentNav = page;
        switch (page)
        {
            case NavPage.Dashboard:
                CurrentPage = Dashboard;
                await Dashboard.LoadAsync();
                break;
            case NavPage.Chat:
                CurrentPage = Chat;
                break;
            case NavPage.Meetings:
                CurrentPage = Meetings;
                await Meetings.RefreshAsync();
                break;
            case NavPage.Settings:
                CurrentPage = Settings;
                Settings.General.RefreshAgentStatus();
                break;
        }
    }

    public Task ShowMeetingAsync(string meetingId)
    {
        CurrentNav = NavPage.Meetings;
        CurrentPage = Meetings;
        return Meetings.OpenAsync(meetingId);
    }

    [RelayCommand]
    private async Task NewChatAsync()
    {
        SelectedConversation = null;
        await Chat.StartNewAsync();
        CurrentNav = NavPage.Chat;
        CurrentPage = Chat;
    }

    [RelayCommand]
    private async Task OpenConversationAsync(Conversation conversation)
    {
        SelectedConversation = conversation;
        await Chat.OpenAsync(conversation);
        CurrentNav = NavPage.Chat;
        CurrentPage = Chat;
    }

    [RelayCommand]
    private async Task DeleteConversationAsync(Conversation conversation)
    {
        await _services.Conversations.DeleteAsync(conversation.Id);
        if (Chat.Conversation?.Id == conversation.Id) await Chat.StartNewAsync();
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;

    [RelayCommand]
    public void TogglePause()
    {
        IsPaused = !IsPaused;
        _services.Agent.SetPaused(IsPaused);
        _services.Tray.SetPaused(IsPaused);
        RefreshStatus();
    }

    private async Task ReloadConversationsAsync()
    {
        var list = await _services.Conversations.FetchActiveConversationsAsync();
        Conversations.Clear();
        foreach (var conversation in list) Conversations.Add(conversation);
    }

    private void RefreshStatus()
    {
        var shared = FocusLens.Core.Settings.SharedState.Read();
        IsPaused = shared.TrackingPaused;
        _services.Tray.SetPaused(IsPaused);

        AgentStatusText = _services.Agent.Status() switch
        {
            AgentStatus.Running => "Tracking",
            AgentStatus.Paused => "Paused",
            _ => "Agent stopped",
        };

        var fresh = shared.RecordingStatusAt > 0 &&
                    FocusLens.Core.Settings.SharedState.NowUnix() - shared.RecordingStatusAt < 3600;
        RestrictionNotice = shared.RecordingRestricted && fresh
            ? $"Not recording {shared.RecordingRestrictedApp}: {shared.RecordingRestrictedReason}"
            : null;
    }
}
