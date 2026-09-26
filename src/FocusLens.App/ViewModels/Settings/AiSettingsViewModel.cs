using System.Collections.ObjectModel;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusLens.Core.Ai;
using FocusLens.Core.Ai.Streams;

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
    [ObservableProperty] private bool _hasNvidiaKey;
    [ObservableProperty] private bool _hasDeepSeekKey;
    [ObservableProperty] private string? _testResult;
    [ObservableProperty] private bool _isTesting;
    [ObservableProperty] private bool _isLoadingModels;
    [ObservableProperty] private string? _modelsHint;

    /// <summary>Models installed in Ollama, for the searchable dropdown. Free text is still accepted.</summary>
    public ObservableCollection<string> AvailableModels { get; } = new();

    public IReadOnlyList<string> Providers { get; } = new[] { "Ollama (local, private)", "Claude", "OpenAI", "NVIDIA", "DeepSeek" };
    public bool IsOllama => ProviderIndex == 0;
    public bool IsClaude => ProviderIndex == 1;
    public bool IsOpenAi => ProviderIndex == 2;
    public bool IsNvidia => ProviderIndex == 3;
    public bool IsDeepSeek => ProviderIndex == 4;
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
            AiProviderKind.Nvidia => 3,
            AiProviderKind.DeepSeek => 4,
            _ => 0,
        };
        _ollamaHost = settings.OllamaHost;
        _ollamaModel = settings.OllamaModel;
        _hasClaudeKey = !string.IsNullOrEmpty(secrets.Get(AiSettings.SecretNames.Claude));
        _hasOpenAiKey = !string.IsNullOrEmpty(secrets.Get(AiSettings.SecretNames.OpenAi));
        _hasNvidiaKey = !string.IsNullOrEmpty(secrets.Get(AiSettings.SecretNames.Nvidia));
        _hasDeepSeekKey = !string.IsNullOrEmpty(secrets.Get(AiSettings.SecretNames.DeepSeek));
        _ = RefreshModelsAsync();
    }

    partial void OnProviderIndexChanged(int value)
    {
        _settings.Provider = value switch
        {
            1 => AiProviderKind.Claude,
            2 => AiProviderKind.OpenAi,
            3 => AiProviderKind.Nvidia,
            4 => AiProviderKind.DeepSeek,
            _ => AiProviderKind.Ollama,
        };
        _settings.Save();
        OnPropertyChanged(nameof(IsOllama));
        OnPropertyChanged(nameof(IsClaude));
        OnPropertyChanged(nameof(IsOpenAi));
        OnPropertyChanged(nameof(IsNvidia));
        OnPropertyChanged(nameof(IsDeepSeek));
        OnPropertyChanged(nameof(IsCloud));
        TestResult = null;
        if (value == 0) _ = RefreshModelsAsync();
    }

    partial void OnOllamaHostChanged(string value)
    {
        _settings.OllamaHost = value;
        _settings.Save();
        _ = RefreshModelsAsync();
    }
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

    public void SetNvidiaKey(string key)
    {
        _secrets.Set(AiSettings.SecretNames.Nvidia, key.Trim());
        HasNvidiaKey = key.Trim().Length > 0;
    }

    public void SetDeepSeekKey(string key)
    {
        _secrets.Set(AiSettings.SecretNames.DeepSeek, key.Trim());
        HasDeepSeekKey = key.Trim().Length > 0;
    }

    /// <summary>Asks the running Ollama which models are installed. Safe to call any time; failures just empty the list.</summary>
    [RelayCommand]
    public async Task RefreshModelsAsync()
    {
        if (IsLoadingModels) return;
        IsLoadingModels = true;
        try
        {
            var models = await new OllamaClient(OllamaHost).ListModelsAsync();
            AvailableModels.Clear();
            foreach (var model in models) AvailableModels.Add(model);
            ModelsHint = models.Count > 0
                ? $"{models.Count} installed. Type to search."
                : "No models found. Make sure Ollama is running, or set it up under Settings > Local tools.";
        }
        finally
        {
            IsLoadingModels = false;
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        IsTesting = true;
        TestResult = "Testing...";
        try
        {
            TestResult = IsOllama ? await TestOllamaAsync() : await TestKeyAsync();
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

    /// <summary>
    /// Sends the same kind of request chat does, with the default model.
    /// A model list can accept a key while chat returns 410 for a retired model.
    /// </summary>
    private async Task<string> TestKeyAsync()
    {
        var (name, url, model, secret, anthropic) = ProviderIndex switch
        {
            1 => ("Claude", "https://api.anthropic.com/v1/messages", StreamingAiClient.Models.Claude, AiSettings.SecretNames.Claude, true),
            2 => ("OpenAI", "https://api.openai.com/v1/chat/completions", StreamingAiClient.Models.OpenAi, AiSettings.SecretNames.OpenAi, false),
            3 => ("NVIDIA", "https://integrate.api.nvidia.com/v1/chat/completions", StreamingAiClient.Models.Nvidia, AiSettings.SecretNames.Nvidia, false),
            4 => ("DeepSeek", "https://api.deepseek.com/chat/completions", StreamingAiClient.Models.DeepSeek, AiSettings.SecretNames.DeepSeek, false),
            _ => ("", "", "", "", false),
        };
        var key = _secrets.Get(secret) ?? "";
        if (key.Length == 0) return "Enter an API key first.";

        var body = new Dictionary<string, object>
        {
            ["model"] = model,
            ["messages"] = new[] { new Dictionary<string, string> { ["role"] = "user", ["content"] = "Reply with ok" } },
        };
        body["max_tokens"] = 16;

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        if (anthropic)
        {
            request.Headers.Add("x-api-key", key);
            request.Headers.Add("anthropic-version", "2023-06-01");
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        using var response = await http.SendAsync(request);
        if (response.IsSuccessStatusCode) return "Key accepted.";
        var payload = await response.Content.ReadAsStringAsync();
        var code = (int)response.StatusCode;
        if (code is 401 or 403) return "This key was rejected.";
        return ChatStreamHelpers.ProviderMessage(payload) ?? $"{name} answered HTTP {code}.";
    }

    private async Task<string> TestOllamaAsync()
    {
        var reply = await _client.CompleteAsync("Reply with the single word: ok");
        return reply.Trim().Length > 0 ? "Connected." : "The model returned an empty reply.";
    }
}
