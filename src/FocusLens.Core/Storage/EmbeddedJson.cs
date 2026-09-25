using System.Reflection;
using System.Text.Json;

namespace FocusLens.Core.Storage;

/// <summary>Reads JSON files embedded in the Core assembly (Resources\*.json).</summary>
public static class EmbeddedJson
{
    public static T? Load<T>(string fileName)
    {
        var assembly = typeof(EmbeddedJson).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase));
        if (resource is null) return default;

        using var stream = assembly.GetManifestResourceStream(resource);
        return stream is null ? default : JsonSerializer.Deserialize<T>(stream, JsonFile.Options);
    }
}
