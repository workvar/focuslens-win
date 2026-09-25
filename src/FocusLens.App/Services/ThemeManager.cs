using System.Windows;
using Microsoft.Win32;

namespace FocusLens.App.Services;

/// <summary>
/// Owns light, dark and glass. Swaps the color dictionary at runtime (brushes follow through DynamicResource),
/// layers the glass alpha on top, and keeps the main window's backdrop and title bar in step.
/// </summary>
public static class ThemeManager
{
    private const string LightSource = "Themes/Theme.Light.xaml";
    private const string DarkSource = "Themes/Theme.Dark.xaml";

    private static AppSettings? _settings;
    private static Window? _window;
    private static bool _hooked;

    /// <summary>Raised after the theme changes so custom-drawn controls can redraw.</summary>
    public static event Action? Changed;

    /// <summary>True when the dark palette is showing, whether chosen or inherited from Windows.</summary>
    public static bool IsDark { get; private set; }

    /// <summary>True when the acrylic backdrop is actually active (setting on and the OS supports it).</summary>
    public static bool IsGlassActive { get; private set; }

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

    /// <summary>Applies the appearance and glass settings. Call again after either one changes.</summary>
    public static void Apply(AppSettings settings)
    {
        _settings = settings;
        HookSystemTheme();

        IsDark = settings.Appearance switch
        {
            AppearanceMode.Dark => true,
            AppearanceMode.Light => false,
            _ => SystemPrefersDark(),
        };
        IsGlassActive = settings.LiquidGlass && WindowBackdrop.IsSupported;

        var resources = Application.Current.Resources;
        SwapPalette(resources.MergedDictionaries, IsDark);
        GlassPalette.Apply(resources, IsGlassActive, settings.GlassTransparency);
        if (_window is not null) WindowBackdrop.Apply(_window, IsDark, IsGlassActive);

        Changed?.Invoke();
    }

    /// <summary>Registers the window whose backdrop and title bar follow the theme.</summary>
    public static void Attach(Window window)
    {
        _window = window;
        WindowBackdrop.Apply(window, IsDark, IsGlassActive);
    }

    private static void SwapPalette(IList<ResourceDictionary> merged, bool dark)
    {
        var existing = merged.FirstOrDefault(d => d.Source is { } s &&
            (s.OriginalString.EndsWith("Theme.Light.xaml") || s.OriginalString.EndsWith("Theme.Dark.xaml")));
        var next = new ResourceDictionary { Source = new Uri(dark ? DarkSource : LightSource, UriKind.Relative) };

        if (existing is null) merged.Insert(0, next);
        else merged[merged.IndexOf(existing)] = next;
    }

    /// <summary>In "Match system" mode, follow Windows when the user flips its light or dark setting.</summary>
    private static void HookSystemTheme()
    {
        if (_hooked) return;
        _hooked = true;
        SystemEvents.UserPreferenceChanged += (_, args) =>
        {
            if (args.Category != UserPreferenceCategory.General || _settings?.Appearance != AppearanceMode.System) return;
            Application.Current?.Dispatcher.InvokeAsync(() => Apply(_settings));
        };
    }
}
