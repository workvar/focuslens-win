using System.Text.Json.Serialization;
using FocusLens.Core.Ai;
using FocusLens.Core.Paths;
using FocusLens.Core.Storage;

namespace FocusLens.Core.Guide;

/// <summary>
/// Guide preferences (guide-settings.json). The Settings tab edits the instance; the hotkey
/// listener and the cursor read it, and <see cref="Changed"/> tells them to re-register.
/// </summary>
public sealed class GuideSettings
{
    /// <summary>Win32 modifier bits: 1 alt, 2 control, 4 shift, 8 win.</summary>
    public const int ModAlt = 1, ModControl = 2, ModShift = 4, ModWin = 8;

    public bool Enabled { get; set; } = true;

    /// <summary>Default shortcut: control + alt + G.</summary>
    public int HotkeyModifiers { get; set; } = ModControl | ModAlt;
    public int HotkeyVirtualKey { get; set; } = 0x47;
    public string HotkeyDisplay { get; set; } = "Ctrl+Alt+G";

    /// <summary>1 is a lazy trail, 5 is nearly glued to the pointer.</summary>
    public int FollowLevel { get; set; } = 3;
    public bool ShowTag { get; set; } = true;
    /// <summary>Optional Ollama model. Empty uses the model from the AI tab. Used only while Ollama is the provider.</summary>
    public string Model { get; set; } = "";

    /// <summary>Optional Claude model id. Empty uses Claude Sonnet. Used only while Anthropic is the provider.</summary>
    public string AnthropicModel { get; set; } = "";

    /// <summary>Optional OpenAI model id. Empty uses gpt-4o-mini. Used only while OpenAI is the provider.</summary>
    public string OpenAiModel { get; set; } = "";

    /// <summary>Optional NVIDIA model id. Empty uses Llama 3.3 70B. Used only while NVIDIA is the provider.</summary>
    public string NvidiaModel { get; set; } = "";

    /// <summary>Optional DeepSeek model id. Empty uses deepseek-flash. Used only while DeepSeek is the provider.</summary>
    public string DeepSeekModel { get; set; } = "";

    /// <summary>Off by default: the request text leaves the PC when a search runs.</summary>
    public bool WebSearch { get; set; }

    /// <summary>Optional SearXNG instance tried before DuckDuckGo. Empty uses DuckDuckGo only.</summary>
    public string SearxUrl { get; set; } = "";

    /// <summary>Hold to fill: rest the pointer on an empty search field to have a suggestion typed in.</summary>
    public bool HoldFill { get; set; } = true;

    /// <summary>How long the pointer must stay still before the text goes in, 0.5 to 4 seconds.</summary>
    public double HoldFillSeconds { get; set; } = 1.5;

    public const double HoldFillMin = 0.5, HoldFillMax = 4;

    public event Action? Changed;

    [JsonIgnore] public TimeSpan HoldFillDuration => TimeSpan.FromSeconds(Math.Clamp(HoldFillSeconds, HoldFillMin, HoldFillMax));

    [JsonIgnore] public double FollowRate => 4.0 + Math.Clamp(FollowLevel, 1, 5) * 3.0;
    [JsonIgnore] public string? PlannerModel => Blank(Model);

    /// <summary>
    /// A short planning or hold-to-fill request. The model override is the one stored for the provider
    /// that is actually enabled, so an Ollama name is never sent to Claude or OpenAI.
    /// </summary>
    public AiRequestOptions ForRequest(AiProvider provider, int maxTokens, double temperature)
    {
        var options = new AiRequestOptions
        {
            MaxTokens = maxTokens,
            Temperature = temperature,
            DisableThinking = true,
            KeepAlive = "10m",
        };
        return provider switch
        {
            AiProvider.Claude => options with { Model = Blank(AnthropicModel) },
            AiProvider.OpenAi => options with { Model = Blank(OpenAiModel) },
            AiProvider.Nvidia => options with { Model = Blank(NvidiaModel) },
            AiProvider.DeepSeek => options with { Model = Blank(DeepSeekModel) },
            _ => options with { OllamaModel = PlannerModel },
        };
    }

    private static string? Blank(string? value)
    {
        var trimmed = value?.Trim() ?? "";
        return trimmed.Length == 0 ? null : trimmed;
    }

    public static GuideSettings Load() => JsonFile.Load(AppPaths.GuideSettingsFile, () => new GuideSettings());

    public void Save()
    {
        JsonFile.Save(AppPaths.GuideSettingsFile, this);
        Changed?.Invoke();
    }
}
