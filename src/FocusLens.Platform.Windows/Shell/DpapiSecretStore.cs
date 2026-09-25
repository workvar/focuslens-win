using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FocusLens.Core.Ai;
using FocusLens.Core.Paths;

namespace FocusLens.Platform.Windows.Shell;

/// <summary>
/// Stores secrets encrypted with DPAPI (current-user scope), so they can only be decrypted by this
/// Windows account on this PC. Stored in %LOCALAPPDATA%\FocusLens\secrets.bin.
/// </summary>
public sealed class DpapiSecretStore : ISecretStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("FocusLens.Secrets.v1");
    private readonly object _gate = new();
    private readonly string _path = Path.Combine(AppPaths.Root, "secrets.bin");

    public string? Get(string name)
    {
        lock (_gate) return Load().GetValueOrDefault(name);
    }

    public void Set(string name, string? value)
    {
        lock (_gate)
        {
            var all = Load();
            if (string.IsNullOrEmpty(value)) all.Remove(name);
            else all[name] = value;
            Save(all);
        }
    }

    private Dictionary<string, string> Load()
    {
        try
        {
            if (!File.Exists(_path)) return new();
            var plain = ProtectedData.Unprotect(File.ReadAllBytes(_path), Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(plain) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private void Save(Dictionary<string, string> all)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(all);
        var protectedBytes = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
        var temp = _path + ".tmp";
        File.WriteAllBytes(temp, protectedBytes);
        File.Move(temp, _path, overwrite: true);
    }
}
