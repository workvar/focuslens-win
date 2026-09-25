using FocusLens.Core.Paths;
using FocusLens.Core.Storage;

namespace FocusLens.Core.Settings;

/// <summary>
/// Which kinds of data FocusLens may collect. Stored as tracking.json. The app
/// edits it and the agent re-reads it every few seconds.
/// </summary>
public sealed class TrackingSettings
{
    /// <summary>Only items the user changed are stored; a missing entry means "on".</summary>
    public Dictionary<string, bool> Overrides { get; set; } = new();

    public bool IsSet(TrackingItem item) =>
        !Overrides.TryGetValue(item.Key(), out var value) || value;

    public bool IsOn(TrackingItem item)
    {
        if (!IsSet(item)) return false;
        var parent = item.Requires();
        return parent is null || IsOn(parent.Value);
    }

    public void Set(TrackingItem item, bool on)
    {
        if (on) Overrides.Remove(item.Key());
        else Overrides[item.Key()] = false;
    }

    public static TrackingSettings Load() =>
        JsonFile.Load(AppPaths.TrackingFile, () => new TrackingSettings());

    public bool Save() => JsonFile.Save(AppPaths.TrackingFile, this);
}
