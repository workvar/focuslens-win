using System.Net;
using System.Text.Json;

namespace FocusLens.Core.Health;

/// <summary>
/// Checks the active model provider. Ollama is asked for its model list; Claude and OpenAI are asked
/// for their model list too, which validates the key without spending tokens or sending any user data.
/// </summary>
public sealed class LlmProbe : IHealthProbe
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };

    private readonly Func<Ai.AiSettings> _settings;
    private readonly Ai.ISecretStore _secrets;

    public LlmProbe(Func<Ai.AiSettings> settings, Ai.ISecretStore secrets)
    {
        _settings = settings;
        _secrets = secrets;
    }

    public string Name => "LLM model";

    /// <summary>Local checks are cheap; cloud checks are spaced out to stay polite to the provider.</summary>
    public TimeSpan MinInterval =>
        _settings().Provider == Ai.AiProviderKind.Ollama ? TimeSpan.FromSeconds(15) : TimeSpan.FromMinutes(2);

    public Task<ServiceHealth> CheckAsync(CancellationToken ct)
    {
        var settings = _settings();
        return settings.Provider switch
        {
            Ai.AiProviderKind.Claude => CheckCloudAsync(
                "Claude", "https://api.anthropic.com/v1/models", Key(Ai.AiSettings.SecretNames.Claude),
                (request, key) =>
                {
                    request.Headers.Add("x-api-key", key);
                    request.Headers.Add("anthropic-version", "2023-06-01");
                }, ct),
            Ai.AiProviderKind.OpenAi => CheckCloudAsync(
                "OpenAI", "https://api.openai.com/v1/models", Key(Ai.AiSettings.SecretNames.OpenAi),
                (request, key) => request.Headers.Authorization = new("Bearer", key), ct),
            _ => CheckOllamaAsync(settings, ct),
        };
    }

    private string Key(string secretName) =>
        _secrets.Get(secretName) ?? Environment.GetEnvironmentVariable("FOCUSLENS_LLM_KEY") ?? "";

    private async Task<ServiceHealth> CheckOllamaAsync(Ai.AiSettings settings, CancellationToken ct)
    {
        var host = settings.OllamaHost.Trim().TrimEnd('/');
        if (!Uri.TryCreate($"{host}/api/tags", UriKind.Absolute, out var url))
            return ServiceHealth.Disconnected(Name, $"Invalid Ollama host: {host}");

        try
        {
            using var response = await Http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return ServiceHealth.Degraded(Name, $"Ollama answered {(int)response.StatusCode}");

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var installed = new List<string>();
            if (doc.RootElement.TryGetProperty("models", out var models))
                foreach (var model in models.EnumerateArray())
                    if (model.TryGetProperty("name", out var name) && name.GetString() is { } value)
                        installed.Add(value);

            var wanted = settings.OllamaModel.Trim();
            if (wanted.Length == 0)
                return ServiceHealth.Degraded(Name, "Ollama is running, but no model is selected in Settings > AI");
            if (!installed.Any(n => IsSameModel(n, wanted)))
                return ServiceHealth.Degraded(Name, $"Ollama is running, but '{wanted}' is not downloaded yet. Get it under Settings > Local tools, or run: ollama pull {wanted}");
            return ServiceHealth.Connected(Name, $"Ollama, {wanted}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return ServiceHealth.Disconnected(Name, $"Ollama is not reachable at {host}");
        }
    }

    /// <summary>"llama3.1" matches "llama3.1:latest" and any other tag of the same model.</summary>
    private static bool IsSameModel(string installed, string wanted) =>
        installed.Equals(wanted, StringComparison.OrdinalIgnoreCase) ||
        installed.StartsWith(wanted + ":", StringComparison.OrdinalIgnoreCase);

    private async Task<ServiceHealth> CheckCloudAsync(
        string provider, string url, string key, Action<HttpRequestMessage, string> addAuth, CancellationToken ct)
    {
        if (key.Length == 0)
            return ServiceHealth.Disconnected(Name, $"{provider} selected, but no API key is saved");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            addAuth(request, key);
            using var response = await Http.SendAsync(request, ct);
            return response.StatusCode switch
            {
                HttpStatusCode.OK => ServiceHealth.Connected(Name, $"{provider}, API key accepted"),
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                    ServiceHealth.Disconnected(Name, $"{provider} rejected the API key"),
                _ => ServiceHealth.Degraded(Name, $"{provider} answered {(int)response.StatusCode}"),
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return ServiceHealth.Disconnected(Name, $"Cannot reach {new Uri(url).Host}");
        }
    }
}
