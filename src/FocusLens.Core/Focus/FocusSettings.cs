using System.Text.Json.Serialization;
using FocusLens.Core.Paths;
using FocusLens.Core.Storage;

namespace FocusLens.Core.Focus;

/// <summary>How much animation and shadow the focus surfaces use.</summary>
public enum FocusVisualEffects
{
    /// <summary>Minimal while battery saver is on or Windows animations are off, full otherwise.</summary>
    Automatic,
    Full,
    /// <summary>No animations or shadows, and the clock ticks once a minute.</summary>
    Minimal,
}

/// <summary>
/// Focus Mode preferences (focus-settings.json). The Focus page and Settings > Focus edit the
/// same instance, so either can change them; <see cref="Changed"/> tells the other one.
/// Readers clamp, so a hand-edited file cannot push a value out of range.
/// </summary>
public sealed class FocusSettings
{
    public const int DefaultCountdown = 5;
    public const int DefaultPatience = 45;
    public const int DefaultPoll = 6;
    /// <summary>Offered in the picker. Longer is lighter; app switches are caught at once anyway.</summary>
    public static readonly int[] PollChoices = { 2, 4, 6, 10, 15, 30 };

    public bool ShowWidget { get; set; } = true;
    /// <summary>Minutes left in the tray icon's tooltip and menu. The Windows stand-in for the Mac menu bar timer.</summary>
    public bool ShowTrayTimer { get; set; } = true;
    public int CountdownSeconds { get; set; } = DefaultCountdown;
    /// <summary>Comma or newline separated apps or sites never treated as a distraction.</summary>
    public string Allowlist { get; set; } = "";
    public FocusEnforcement Enforcement { get; set; } = FocusEnforcement.Close;
    public int PatienceSeconds { get; set; } = DefaultPatience;
    public int PollSeconds { get; set; } = DefaultPoll;
    public bool PauseWhenIdle { get; set; } = true;
    public FocusVisualEffects VisualEffects { get; set; } = FocusVisualEffects.Automatic;
    /// <summary>An Ollama model just for focus checks. Empty uses the AI tab's model.</summary>
    public string Model { get; set; } = "";
    /// <summary>Where the user left the floating widget (top-left corner), if they moved it.</summary>
    public double? WidgetLeft { get; set; }
    public double? WidgetTop { get; set; }

    /// <summary>Raised after <see cref="Save"/>.</summary>
    public event Action? Changed;

    [JsonIgnore] public int PollInterval => Math.Clamp(PollSeconds, 2, 30);
    [JsonIgnore] public int Countdown => Math.Clamp(CountdownSeconds, 3, 15);
    [JsonIgnore] public int Patience => Math.Clamp(PatienceSeconds, 15, 180);
    [JsonIgnore] public string? FocusModel => string.IsNullOrWhiteSpace(Model) ? null : Model.Trim();

    [JsonIgnore] public IReadOnlyList<string> AllowlistEntries => Allowlist
        .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(e => e.ToLowerInvariant())
        .ToList();

    public static FocusSettings Load() => JsonFile.Load(AppPaths.FocusSettingsFile, () => new FocusSettings());

    public void Save()
    {
        JsonFile.Save(AppPaths.FocusSettingsFile, this);
        Changed?.Invoke();
    }
}
