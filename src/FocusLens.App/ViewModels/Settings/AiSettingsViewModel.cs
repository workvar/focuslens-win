using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLens.Core.Ai;

namespace FocusLens.App.ViewModels.Settings;

/// <summary>Model provider choice, API keys (encrypted per Windows account) and meeting summary policy.</summary>
public sealed partial class AiSettingsViewModel : ObservableObject
{
    private readonly AiSettings _settings;
    private readonly ISecretStore _secrets;
    private readonly StreamingAiClient _client;

    [ObservableProperty] private int _providerIndex;
    [ObservableProperty] private string _ollamaHost;
    [ObservableProperty] private string _ollamaModel;
    [ObservableProperty] private bool _hasClaudeKey;
    [ObservableProperty] private bool _hasOpenAiKey;
    [ObservableProperty] private string? _testResult;
    [ObservableProperty] private bool _isTesting;

    public IReadOnlyList<string> Providers { get; } = new[] { "Ollama (local, private)", "Claude", "OpenAI" };
    public bool IsOllama => ProviderIndex == 0;
    public bool IsClaude => ProviderIndex == 1;
    public bool IsOpenAi => ProviderIndex == 2;
    public bool IsCloud => ProviderIndex != 0;

    public AiSettingsViewModel(AiSettings settings, ISecretStore secrets, StreamingAiClient client)
    {
        _settings = settings;
        _secrets = secrets;
        _client = client;
        _providerIndex = settings.Provider switch
        {
            AiProviderKind.Claude => 1,
            AiProviderKind.OpenAi => 2,
            _ => 0,
        };
        _ollamaHost = settings.OllamaHost;
        _ollamaModel = settings.OllamaModel;
        _hasClaudeKey = !string.IsNullOrEmpty(secrets.Get(AiSettings.SecretNames.Claude));
        _hasOpenAiKey = !string.IsNullOrEmpty(secrets.Get(AiSettings.SecretNames.OpenAi));
    }

    partial void OnProviderIndexChanged(int value)
    {
        _settings.Provider = value switch { 1 => AiProviderKind.Claude, 2 => AiProviderKind.OpenAi, _ => AiProviderKind.Ollama };
        _settings.Save();
        OnPropertyChanged(nameof(IsOllama));
        OnPropertyChanged(nameof(IsClaude));
        OnPropertyChanged(nameof(IsOpenAi));
        OnPropertyChanged(nameof(IsCloud));
        TestResult = null;
    }

    partial void OnOllamaHostChanged(string value) { _settings.OllamaHost = value; _settings.Save(); }
    partial void OnOllamaModelChanged(string value) { _settings.OllamaModel = value; _settings.Save(); }

    /// <summary>Called from the password boxes, which cannot bind their content.</summary>
    public void SetClaudeKey(string key)
    {
        _secrets.Set(AiSettings.SecretNames.Claude, key.Trim());
        HasClaudeKey = key.Trim().Length > 0;
    }

    public void SetOpenAiKey(string key)
    {
        _secrets.Set(AiSettings.SecretNames.OpenAi, key.Trim());
        HasOpenAiKey = key.Trim().Length > 0;
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        IsTesting = true;
        TestResult = "Testing...";
        try
        {
            var reply = await _client.CompleteAsync("Reply with the single word: ok");
            TestResult = reply.Trim().Length > 0 ? "Connected." : "The model returned an empty reply.";
        }
        catch (Exception ex)
        {
            TestResult = ex.Message;
        }
        finally
        {
            IsTesting = false;
        }
    }
}
