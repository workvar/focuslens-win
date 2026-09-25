using CommunityToolkit.Mvvm.ComponentModel;

namespace FocusLens.App.ViewModels.Settings;

/// <summary>Hosts the settings tabs.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    public GeneralSettingsViewModel General { get; }
    public TrackingSettingsViewModel Tracking { get; }
    public PrivacySettingsViewModel Privacy { get; }
    public AiSettingsViewModel Ai { get; }
    public MeetingSettingsViewModel Meetings { get; }

    public SettingsViewModel(
        GeneralSettingsViewModel general, TrackingSettingsViewModel tracking, PrivacySettingsViewModel privacy,
        AiSettingsViewModel ai, MeetingSettingsViewModel meetings)
    {
        General = general;
        Tracking = tracking;
        Privacy = privacy;
        Ai = ai;
        Meetings = meetings;
    }
}
