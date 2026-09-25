using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLens.App.Services;

namespace FocusLens.App.ViewModels.Settings;

/// <summary>Light, dark or system, plus the liquid glass effect. Also drives the sidebar's quick theme button.</summary>
public sealed partial class AppearanceSettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;

    [ObservableProperty] private AppearanceMode _mode;
    [ObservableProperty] private bool _liquidGlass;
    [ObservableProperty] private double _glassTransparency;

    public AppearanceSettingsViewModel(AppSettings settings)
    {
        _settings = settings;
        _mode = settings.Appearance;
        _liquidGlass = settings.LiquidGlass;
        _glassTransparency = settings.GlassTransparency;
        ThemeManager.Changed += OnThemeChanged;
    }

    public bool IsSystem { get => Mode == AppearanceMode.System; set { if (value) Mode = AppearanceMode.System; } }
    public bool IsLight { get => Mode == AppearanceMode.Light; set { if (value) Mode = AppearanceMode.Light; } }
    public bool IsDark { get => Mode == AppearanceMode.Dark; set { if (value) Mode = AppearanceMode.Dark; } }

    public bool GlassSupported => WindowBackdrop.IsSupported;
    public bool GlassAvailable => GlassSupported && LiquidGlass;
    public string GlassPercent => $"{Math.Round(GlassTransparency * 100)}%";
    public string GlassNote => GlassSupported
        ? "Translucent acrylic surfaces across the app. Turn off for a flat, opaque look."
        : "Liquid glass needs Windows 11 version 22H2 or newer. This PC shows the flat look.";

    /// <summary>The sidebar button offers the opposite of what is showing: a sun in dark mode, a moon in light mode.</summary>
    public string ToggleGlyph => ThemeManager.IsDark ? "" : "";
    public string ToggleLabel => ThemeManager.IsDark ? "Light mode" : "Dark mode";

    private void OnThemeChanged()
    {
        OnPropertyChanged(nameof(ToggleGlyph));
        OnPropertyChanged(nameof(ToggleLabel));
    }

    partial void OnModeChanged(AppearanceMode value)
    {
        _settings.Appearance = value;
        Commit();
        OnPropertyChanged(nameof(IsSystem));
        OnPropertyChanged(nameof(IsLight));
        OnPropertyChanged(nameof(IsDark));
    }

    partial void OnLiquidGlassChanged(bool value)
    {
        _settings.LiquidGlass = value;
        Commit();
        OnPropertyChanged(nameof(GlassAvailable));
    }

    partial void OnGlassTransparencyChanged(double value)
    {
        _settings.GlassTransparency = value;
        Commit();
        OnPropertyChanged(nameof(GlassPercent));
    }

    [RelayCommand]
    private void ToggleLightDark() => Mode = ThemeManager.IsDark ? AppearanceMode.Light : AppearanceMode.Dark;

    private void Commit()
    {
        _settings.Save();
        ThemeManager.Apply(_settings);
        OnPropertyChanged(nameof(ToggleGlyph));
        OnPropertyChanged(nameof(ToggleLabel));
    }
}
