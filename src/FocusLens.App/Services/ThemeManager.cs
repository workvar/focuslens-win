using System.Windows;
using Microsoft.Win32;

namespace FocusLens.App.Services;

/// <summary>Swaps the light or dark color dictionary at runtime; all brushes update through DynamicResource.</summary>
public static class ThemeManager
{
    private const string LightSource = "Themes/Theme.Light.xaml";
    private const string DarkSource = "Themes/Theme.Dark.xaml";

    /// <summary>Raised after the color dictionary changes so custom-drawn controls can redraw.</summary>
    public static event Action? Changed;

    public static bool SystemPrefersDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }

    public static void Apply(AppearanceMode mode)
    {
        var dark = mode switch
        {
            AppearanceMode.Dark => true,
            AppearanceMode.Light => false,
            _ => SystemPrefersDark(),
        };

        var merged = Application.Current.Resources.MergedDictionaries;
        var existing = merged.FirstOrDefault(d => d.Source is { } s &&
            (s.OriginalString.EndsWith("Theme.Light.xaml") || s.OriginalString.EndsWith("Theme.Dark.xaml")));
        var next = new ResourceDictionary { Source = new Uri(dark ? DarkSource : LightSource, UriKind.Relative) };

        if (existing is null) merged.Insert(0, next);
        else merged[merged.IndexOf(existing)] = next;
        Changed?.Invoke();
    }
}
