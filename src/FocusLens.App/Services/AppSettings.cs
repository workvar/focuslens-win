using FocusLens.Core.Paths;
using FocusLens.Core.Storage;

namespace FocusLens.App.Services;

public enum AppearanceMode
{
    System,
    Light,
    Dark,
}

/// <summary>UI preferences and optional Supabase configuration (app-settings.json).</summary>
public sealed class AppSettings
{
    public bool OnboardingCompleted { get; set; }
    public bool SidebarCollapsed { get; set; }
    public AppearanceMode Appearance { get; set; } = AppearanceMode.System;
    public bool StartWithWindows { get; set; } = true;

    /// <summary>Acrylic backdrop and translucent surfaces. Off by default, like the Mac app.</summary>
    public bool LiquidGlass { get; set; }

    /// <summary>0 = opaque surfaces, 1 = most see-through. Same scale and default as the Mac slider.</summary>
    public double GlassTransparency { get; set; } = 0.6;

    /// <summary>Optional. Sign-in is available only when both values are set (or the env vars below).</summary>
    public string? SupabaseUrl { get; set; }
    public string? SupabaseAnonKey { get; set; }

    public string? ResolvedSupabaseUrl =>
        Environment.GetEnvironmentVariable("FOCUSLENS_SUPABASE_URL") ?? SupabaseUrl;

    public string? ResolvedSupabaseAnonKey =>
        Environment.GetEnvironmentVariable("FOCUSLENS_SUPABASE_ANON_KEY") ?? SupabaseAnonKey;

    public bool IsAuthConfigured =>
        !string.IsNullOrWhiteSpace(ResolvedSupabaseUrl) && !string.IsNullOrWhiteSpace(ResolvedSupabaseAnonKey);

    public static AppSettings Load() => JsonFile.Load(AppPaths.AppSettingsFile, () => new AppSettings());
    public void Save() => JsonFile.Save(AppPaths.AppSettingsFile, this);
}
