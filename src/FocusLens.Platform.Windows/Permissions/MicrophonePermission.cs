using FocusLens.Core.Permissions;
using Microsoft.Win32;
using NAudio.CoreAudioApi;

namespace FocusLens.Platform.Windows.Permissions;

/// <summary>
/// Microphone access for meeting notes. Windows keeps three switches: a device-wide one (HKLM, set by admins),
/// the user's "Microphone access", and "Let desktop apps access your microphone". Any of them off blocks capture.
/// </summary>
public sealed class MicrophonePermission : IPermissionCheck
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";

    public string Id => "microphone";
    public string Name => "Microphone";
    public string Purpose => "Records your side of a meeting when you take notes.";
    public string GrantLabel => "Open Settings";

    public Task<PermissionStatus> CheckAsync()
    {
        if (Read(Registry.LocalMachine, Key) == "Deny")
            return Task.FromResult(new PermissionStatus(PermissionState.Denied, "Microphone access is turned off for this device by an administrator."));
        if (Read(Registry.CurrentUser, Key) == "Deny")
            return Task.FromResult(new PermissionStatus(PermissionState.Denied, "Microphone access is turned off for your account."));
        if (Read(Registry.CurrentUser, Key + @"\NonPackaged") == "Deny")
            return Task.FromResult(new PermissionStatus(PermissionState.Denied, "\"Let desktop apps access your microphone\" is turned off."));
        if (!HasDevice())
            return Task.FromResult(new PermissionStatus(PermissionState.Unavailable, "No microphone was found. Plug one in or enable it in Sound settings."));
        return Task.FromResult(new PermissionStatus(PermissionState.Granted, "Allowed."));
    }

    public Task GrantAsync() => SettingsPages.OpenAsync(SettingsPages.Microphone);

    private static string? Read(RegistryKey root, string path)
    {
        try
        {
            using var key = root.OpenSubKey(path);
            return key?.GetValue("Value") as string;
        }
        catch
        {
            return null; // unreadable means "not restricted" as far as we can tell
        }
    }

    private static bool HasDevice()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).Count > 0;
        }
        catch
        {
            return true; // if the device list cannot be read, do not report a false problem
        }
    }
}
