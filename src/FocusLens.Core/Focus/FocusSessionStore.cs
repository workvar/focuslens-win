using FocusLens.Core.Paths;
using FocusLens.Core.Storage;

namespace FocusLens.Core.Focus;

/// <summary>
/// Saves finished sessions as one JSON file (focus-sessions.json). Small on purpose: a session
/// is a few kilobytes, so a file is enough. Used from the UI thread only.
/// </summary>
public sealed class FocusSessionStore
{
    private readonly string _path;
    private readonly List<FocusSessionRecord> _sessions;

    public FocusSessionStore(string? path = null)
    {
        _path = path ?? AppPaths.FocusSessionsFile;
        _sessions = JsonFile.Load(_path, () => new List<FocusSessionRecord>());
    }

    /// <summary>Raised after a session is added or deleted.</summary>
    public event Action? Changed;

    /// <summary>Newest first.</summary>
    public IReadOnlyList<FocusSessionRecord> Sessions => _sessions;

    public void Add(FocusSessionRecord record)
    {
        _sessions.Insert(0, record);
        Persist();
    }

    public void Delete(Guid id)
    {
        _sessions.RemoveAll(s => s.Id == id);
        Persist();
    }

    public FocusSessionRecord? Find(Guid id) => _sessions.FirstOrDefault(s => s.Id == id);

    private void Persist()
    {
        JsonFile.Save(_path, _sessions);
        Changed?.Invoke();
    }
}
