using System.Text.Json.Serialization;
using FocusLens.Core.Ai;
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
    /// <summary>An Ollama model just for focus checks. Empty uses the AI tab's model. Used only while Ollama is the provider.</summary>
    public string Model { get; set; } = "";
    /// <summary>Optional Claude model id for focus checks. Empty uses Claude Sonnet. Used only while Anthropic is the provider.</summary>
    public string AnthropicModel { get; set; } = "";
    /// <summary>Optional OpenAI model id for focus checks. Empty uses gpt-4o-mini. Used only while OpenAI is the provider.</summary>
    public string OpenAiModel { get; set; } = "";
    /// <summary>Where the user left the floating widget (top-left corner), if they moved it.</summary>
    public double? WidgetLeft { get; set; }
    public double? WidgetTop { get; set; }

    /// <summary>Raised after <see cref="Save"/>.</summary>
    public event Action? Changed;

    [JsonIgnore] public int PollInterval => Math.Clamp(PollSeconds, 2, 30);
    [JsonIgnore] public int Countdown => Math.Clamp(CountdownSeconds, 3, 15);
    [JsonIgnore] public int Patience => Math.Clamp(PatienceSeconds, 15, 180);
    [JsonIgnore] public string? FocusModel => Blank(Model);

    /// <summary>Classification request with the model override for the provider that is enabled.</summary>
    public AiRequestOptions ClassificationOptions(AiProvider provider)
    {
        var options = AiRequestOptions.Classification;
        return provider switch
        {
            AiProvider.Claude => options with { Model = Blank(AnthropicModel) },
            AiProvider.OpenAi => options with { Model = Blank(OpenAiModel) },
            _ => options with { OllamaModel = FocusModel },
        };
    }

    private static string? Blank(string? value)
    {
        var trimmed = value?.Trim() ?? "";
        return trimmed.Length == 0 ? null : trimmed;
    }

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
