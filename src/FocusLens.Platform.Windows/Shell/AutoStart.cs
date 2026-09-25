using Microsoft.Win32;

namespace FocusLens.Platform.Windows.Shell;

/// <summary>Start-at-login registration through the per-user Run key (no admin rights needed).</summary>
public static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled(string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(valueName) is string;
    }

    public static void Enable(string valueName, string exePath, string arguments = "")
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        key.SetValue(valueName, $"\"{exePath}\" {arguments}".Trim());
    }

    public static void Disable(string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
