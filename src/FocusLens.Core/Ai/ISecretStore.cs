namespace FocusLens.Core.Ai;

/// <summary>
/// Stores secrets (API keys, sign-in tokens). The Windows implementation encrypts with DPAPI;
/// tests can use the in-memory one.
/// </summary>
public interface ISecretStore
{
    string? Get(string name);
    void Set(string name, string? value);
}

public sealed class InMemorySecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _values = new();

    public string? Get(string name) => _values.GetValueOrDefault(name);

    public void Set(string name, string? value)
    {
        if (string.IsNullOrEmpty(value)) _values.Remove(name);
        else _values[name] = value;
    }
}
