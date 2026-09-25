using System.Diagnostics;

namespace FocusLens.Platform.Windows.Permissions;

/// <summary>Opens pages of the Windows Settings app, which is the only place some switches can be flipped.</summary>
internal static class SettingsPages
{
    public const string Microphone = "ms-settings:privacy-microphone";
    public const string Notifications = "ms-settings:notifications";
    public const string Speech = "ms-settings:speech";
    public const string Language = "ms-settings:regionlanguage";

    public static Task OpenAsync(string uri)
    {
        try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); }
        catch { /* Settings could not be launched; the status row stays as it is and the user can retry */ }
        return Task.CompletedTask;
    }
}
