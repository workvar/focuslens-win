using System.Globalization;
using System.Runtime.InteropServices;
using FocusLens.Core.Guide;
using Microsoft.Win32;

namespace FocusLens.Platform.Windows.Guide;

/// <summary>
/// Reads GuideSystem from this PC. Everything here is cheap: three registry values, a culture
/// lookup and two directory listings. The app list is cached for a few minutes, because it is the
/// only part that touches the disk and it almost never changes during a session.
/// </summary>
public static class SystemInfo
{
    private const string CurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
    private const string HttpsUserChoiceKey =
        @"SOFTWARE\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice";

    private static readonly TimeSpan AppsStaleAfter = TimeSpan.FromMinutes(5);
    private static readonly object Gate = new();
    private static IReadOnlyList<string>? _apps;
    private static DateTime _appsReadAt;

    /// <summary>ProgIds are not names anyone would recognise, so the common ones are spelled out.</summary>
    private static readonly Dictionary<string, string> BrowserNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MSEdgeHTM"] = "Microsoft Edge",
        ["ChromeHTML"] = "Google Chrome",
        ["FirefoxURL"] = "Mozilla Firefox",
        ["BraveHTML"] = "Brave",
        ["OperaStable"] = "Opera",
        ["VivaldiHTM"] = "Vivaldi",
        ["AppXq0fevzme2pys62n3e0fbqa7peapykr8v"] = "Microsoft Edge",
    };

    public static GuideSystem Read() => new(
        Os: OsDescription(),
        Device: Device(),
        Language: Language(),
        DefaultBrowser: DefaultBrowser(),
        Apps: InstalledApps());

    // MARK: Pieces

    /// <summary>"Windows 11 Pro 24H2 (build 26100)". The build is what actually decides where a setting lives.</summary>
    private static string OsDescription()
    {
        var product = Value(CurrentVersionKey, "ProductName") ?? "Windows";
        var display = Value(CurrentVersionKey, "DisplayVersion");
        var build = Environment.OSVersion.Version.Build;
        // The registry still says "Windows 10" on Windows 11; the build number is the honest test.
        if (build >= 22000 && product.Contains("Windows 10", StringComparison.OrdinalIgnoreCase))
            product = product.Replace("Windows 10", "Windows 11", StringComparison.OrdinalIgnoreCase);
        var text = product;
        if (display is { Length: > 0 }) text += " " + display;
        return $"{text} (build {build})";
    }

    private static string? Device()
    {
        var parts = new List<string>();
        var maker = Value(@"HARDWARE\DESCRIPTION\System\BIOS", "SystemManufacturer");
        var model = Value(@"HARDWARE\DESCRIPTION\System\BIOS", "SystemProductName");
        if (maker is { Length: > 0 }) parts.Add(maker);
        if (model is { Length: > 0 }) parts.Add(model);
        parts.Add(RuntimeInformation.OSArchitecture.ToString());
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    /// <summary>The language the interface is drawn in, not the number and date formats.</summary>
    private static string? Language()
    {
        try { return CultureInfo.InstalledUICulture.EnglishName; }
        catch { return null; }
    }

    private static string? DefaultBrowser()
    {
        var progId = Value(HttpsUserChoiceKey, "ProgId", RegistryHive.CurrentUser);
        if (progId is not { Length: > 0 }) return null;
        return BrowserNames.TryGetValue(progId, out var name) ? name : progId;
    }

    /// <summary>
    /// Shortcut names from the two Start menu folders, which is what the user sees and what the
    /// taskbar and Start search use. One list, cached.
    /// </summary>
    private static IReadOnlyList<string> InstalledApps()
    {
        lock (Gate)
        {
            if (_apps is { Count: > 0 } && DateTime.UtcNow - _appsReadAt < AppsStaleAfter) return _apps;
            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var folder in new[] { Environment.SpecialFolder.CommonStartMenu, Environment.SpecialFolder.StartMenu })
            {
                var root = Environment.GetFolderPath(folder);
                if (root.Length == 0) continue;
                try
                {
                    foreach (var path in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
                    {
                        var name = Path.GetFileNameWithoutExtension(path);
                        if (name.Length > 0 && !name.Contains("Uninstall", StringComparison.OrdinalIgnoreCase))
                            names.Add(name);
                    }
                }
                catch
                {
                    // A folder this user may not read. Skip it; a shorter list is still useful.
                }
            }
            _apps = names.ToList();
            _appsReadAt = DateTime.UtcNow;
            return _apps;
        }
    }

    private static string? Value(string key, string name, RegistryHive hive = RegistryHive.LocalMachine)
    {
        try
        {
            using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var sub = root.OpenSubKey(key);
            return sub?.GetValue(name) as string;
        }
        catch
        {
            return null;
        }
    }
}
