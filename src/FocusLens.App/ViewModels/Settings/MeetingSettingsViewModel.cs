using CommunityToolkit.Mvvm.ComponentModel;
using FocusLens.Core.Ai;

namespace FocusLens.App.ViewModels.Settings;

/// <summary>Meeting detection and note-taking preferences.</summary>
public sealed partial class MeetingSettingsViewModel : ObservableObject
{
    private readonly AiSettings _settings;

    [ObservableProperty] private bool _detectionEnabled;
    [ObservableProperty] private bool _includeChatApps;
    [ObservableProperty] private bool _allowCloudSummary;
    [ObservableProperty] private bool _keepAudio;
    [ObservableProperty] private string _denyListText;

    public MeetingSettingsViewModel(AiSettings settings)
    {
        _settings = settings;
        _detectionEnabled = settings.MeetingDetectionEnabled;
        _includeChatApps = settings.MeetingDetectionIncludeChatApps;
        _allowCloudSummary = settings.AllowCloudMeetingSummary;
        _keepAudio = settings.KeepMeetingAudio;
        _denyListText = string.Join("\n", settings.MeetingDetectionDenyList);
    }

    partial void OnDetectionEnabledChanged(bool value) { _settings.MeetingDetectionEnabled = value; _settings.Save(); }
    partial void OnIncludeChatAppsChanged(bool value) { _settings.MeetingDetectionIncludeChatApps = value; _settings.Save(); }
    partial void OnAllowCloudSummaryChanged(bool value) { _settings.AllowCloudMeetingSummary = value; _settings.Save(); }
    partial void OnKeepAudioChanged(bool value) { _settings.KeepMeetingAudio = value; _settings.Save(); }

    partial void OnDenyListTextChanged(string value)
    {
        _settings.MeetingDetectionDenyList = value
            .Split(new[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        _settings.Save();
    }
}
