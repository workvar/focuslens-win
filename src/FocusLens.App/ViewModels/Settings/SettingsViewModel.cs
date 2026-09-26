using CommunityToolkit.Mvvm.ComponentModel;
using FocusLens.App.ViewModels.Focus;
using FocusLens.App.ViewModels.Status;
using FocusLens.Core.Guide;

namespace FocusLens.App.ViewModels.Settings;

/// <summary>Hosts the settings tabs.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    public GeneralSettingsViewModel General { get; }
    public AppearanceSettingsViewModel Appearance { get; }
    public TrackingSettingsViewModel Tracking { get; }
    public FocusSettingsViewModel Focus { get; }
    public PrivacySettingsViewModel Privacy { get; }
    public AiSettingsViewModel Ai { get; }
    public MeetingSettingsViewModel Meetings { get; }
    public StatusViewModel Status { get; }
    public PermissionsViewModel Permissions { get; }
    public LocalToolsViewModel LocalTools { get; }
    public ChromaSettingsViewModel Memory { get; }
    public UpdatesSettingsViewModel Updates { get; }

    /// <summary>Guide preferences, shared with the coordinator. Its tab is last so the tab indices above stay valid.</summary>
    public GuideSettings? GuideSettings { get; init; }

    /// <summary>Position of the Updates tab in SettingsView.xaml.</summary>
    public const int UpdatesTab = 11;

    /// <summary>Position of the Local tools tab in SettingsView.xaml.</summary>
    public const int LocalToolsTab = 9;

    /// <summary>Position of the Permissions tab in SettingsView.xaml.</summary>
    public const int PermissionsTab = 8;

    [ObservableProperty] private int _selectedTab;

    public SettingsViewModel(
        GeneralSettingsViewModel general, AppearanceSettingsViewModel appearance, TrackingSettingsViewModel tracking,
        FocusSettingsViewModel focus, PrivacySettingsViewModel privacy, AiSettingsViewModel ai, MeetingSettingsViewModel meetings, StatusViewModel status,
        PermissionsViewModel permissions, LocalToolsViewModel localTools, ChromaSettingsViewModel memory,
        UpdatesSettingsViewModel updates)
    {
        General = general;
        Appearance = appearance;
        Tracking = tracking;
        Focus = focus;
        Privacy = privacy;
        Ai = ai;
        Meetings = meetings;
        Status = status;
        Permissions = permissions;
        LocalTools = localTools;
        Memory = memory;
        Updates = updates;
    }

    /// <summary>
    /// Permissions are first checked at launch, before the app has started the agent, so the tab
    /// said "not running" until the window was re-activated. Opening the tab checks again.
    /// </summary>
    partial void OnSelectedTabChanged(int value)
    {
        if (value == PermissionsTab) _ = Permissions.RefreshAsync();
    }
}
