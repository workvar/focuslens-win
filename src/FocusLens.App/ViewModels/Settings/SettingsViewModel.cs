using CommunityToolkit.Mvvm.ComponentModel;
using FocusLens.App.ViewModels.Status;

namespace FocusLens.App.ViewModels.Settings;

/// <summary>Hosts the settings tabs.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    public GeneralSettingsViewModel General { get; }
    public AppearanceSettingsViewModel Appearance { get; }
    public TrackingSettingsViewModel Tracking { get; }
    public PrivacySettingsViewModel Privacy { get; }
    public AiSettingsViewModel Ai { get; }
    public MeetingSettingsViewModel Meetings { get; }
    public StatusViewModel Status { get; }

    public SettingsViewModel(
        GeneralSettingsViewModel general, AppearanceSettingsViewModel appearance, TrackingSettingsViewModel tracking,
        PrivacySettingsViewModel privacy, AiSettingsViewModel ai, MeetingSettingsViewModel meetings, StatusViewModel status)
    {
        General = general;
        Appearance = appearance;
        Tracking = tracking;
        Privacy = privacy;
        Ai = ai;
        Meetings = meetings;
        Status = status;
    }
}
