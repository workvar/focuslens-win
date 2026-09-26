using FocusLens.Core.Paths;
using FocusLens.Core.Storage;

namespace FocusLens.Core.Ai;

public enum AiProviderKind
{
    Claude,
    OpenAi,
    Ollama,
    Nvidia,
    DeepSeek,
}

/// <summary>
/// Non-secret AI preferences (ai-settings.json). API keys live in the ISecretStore
/// under the names in <see cref="SecretNames"/>.
/// </summary>
public sealed class AiSettings
{
    public AiProviderKind Provider { get; set; } = AiProviderKind.Ollama;
    public string OllamaHost { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "";
    /// <summary>Meeting summaries send the full transcript to a cloud model, so they are opt-in.</summary>
    public bool AllowCloudMeetingSummary { get; set; }
    public bool KeepMeetingAudio { get; set; }
    public bool MeetingDetectionEnabled { get; set; } = true;
    public bool MeetingDetectionIncludeChatApps { get; set; }
    /// <summary>Executable names that must never trigger meeting detection.</summary>
    public List<string> MeetingDetectionDenyList { get; set; } = new();

    public static class SecretNames
    {
        public const string Claude = "ai.claude.apiKey";
        public const string OpenAi = "ai.openai.apiKey";
        public const string Nvidia = "ai.nvidia.apiKey";
        public const string DeepSeek = "ai.deepseek.apiKey";
    }

    public static AiSettings Load() => JsonFile.Load(AppPaths.AiSettingsFile, () => new AiSettings());
    public bool Save() => JsonFile.Save(AppPaths.AiSettingsFile, this);

    /// <summary>Resolves the active provider, falling back to FOCUSLENS_LLM_KEY for a key.</summary>
    public AiProvider Resolve(ISecretStore secrets)
    {
        string Key(string name) =>
            secrets.Get(name) ?? Environment.GetEnvironmentVariable("FOCUSLENS_LLM_KEY") ?? "";

        return Provider switch
        {
            AiProviderKind.Claude => new AiProvider.Claude(Key(SecretNames.Claude)),
            AiProviderKind.OpenAi => new AiProvider.OpenAi(Key(SecretNames.OpenAi)),
            AiProviderKind.Nvidia => new AiProvider.Nvidia(Key(SecretNames.Nvidia)),
            AiProviderKind.DeepSeek => new AiProvider.DeepSeek(Key(SecretNames.DeepSeek)),
            _ => new AiProvider.Ollama(OllamaHost, OllamaModel),
        };
    }
}
