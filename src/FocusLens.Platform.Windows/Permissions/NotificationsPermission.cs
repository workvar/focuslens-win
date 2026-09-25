using FocusLens.Core.Permissions;
using Microsoft.Win32;

namespace FocusLens.Platform.Windows.Permissions;

/// <summary>Toast and balloon notifications: the "meeting detected" prompt and update notices depend on them.</summary>
public sealed class NotificationsPermission : IPermissionCheck
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\PushNotifications";

    public string Id => "notifications";
    public string Name => "Notifications";
    public string Purpose => "Tells you when a meeting is detected, notes are ready, or an update is available.";
    public string GrantLabel => "Open Settings";

    public Task<PermissionStatus> CheckAsync()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(Key);
            // A missing value means notifications are on (the Windows default).
            var enabled = key?.GetValue("ToastEnabled") is not int value || value != 0;
            return Task.FromResult(enabled
                ? new PermissionStatus(PermissionState.Granted, "Allowed.")
                : new PermissionStatus(PermissionState.Denied, "Notifications are turned off for your account."));
        }
        catch
        {
            return Task.FromResult(new PermissionStatus(PermissionState.Granted, "Allowed."));
        }
    }

    public Task GrantAsync() => SettingsPages.OpenAsync(SettingsPages.Notifications);
}
