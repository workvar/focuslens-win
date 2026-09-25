using FocusLens.Core.Paths;
using FocusLens.Core.Storage;

namespace FocusLens.Core.Chroma;

/// <summary>User choices for the semantic search index (chroma-settings.json).</summary>
public sealed class ChromaSettings
{
    /// <summary>Add search matches from the index to AI chat answers.</summary>
    public bool Enabled { get; set; } = true;

    public DateTime? LastIndexedUtc { get; set; }

    public static ChromaSettings Load() => JsonFile.Load(AppPaths.ChromaSettingsFile, () => new ChromaSettings());
    public void Save() => JsonFile.Save(AppPaths.ChromaSettingsFile, this);
}
