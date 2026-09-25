using FocusLens.Core.Permissions;
using FocusLens.Platform.Windows.Audio;
using FocusLens.Platform.Windows.Ocr;

namespace FocusLens.Platform.Windows.Permissions;

/// <summary>Offline transcription needs an installed Windows speech recognizer language.</summary>
public sealed class SpeechRecognizerPermission : IPermissionCheck
{
    public string Id => "speech";
    public string Name => "Speech recognition";
    public string Purpose => "Turns meeting audio into a transcript on this PC. Nothing is uploaded.";
    public string GrantLabel => "Install language";

    public async Task<PermissionStatus> CheckAsync() =>
        await new SystemSpeechTranscriber().RequestAccessAsync()
            ? new PermissionStatus(PermissionState.Granted, "A speech recognizer is installed.")
            : new PermissionStatus(PermissionState.Unavailable, "No speech recognizer is installed for your Windows language.");

    public Task GrantAsync() => SettingsPages.OpenAsync(SettingsPages.Speech);
}

/// <summary>On-device text recognition for screen context needs an OCR-capable language installed.</summary>
public sealed class ScreenTextPermission : IPermissionCheck
{
    public string Id => "screen-text";
    public string Name => "Screen text recognition";
    public string Purpose => "Reads text in the active window to understand what you are working on. Images are never saved.";
    public string GrantLabel => "Install language";

    public Task<PermissionStatus> CheckAsync() =>
        Task.FromResult(new WindowsOcr().IsAvailable
            ? new PermissionStatus(PermissionState.Granted, "Available.")
            : new PermissionStatus(PermissionState.Unavailable, "Windows has no OCR language installed for your profile."));

    public Task GrantAsync() => SettingsPages.OpenAsync(SettingsPages.Language);
}
