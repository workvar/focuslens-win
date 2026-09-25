namespace FocusLens.Core.Paths;

/// <summary>
/// Every on-disk location FocusLens uses. All files live under
/// %LOCALAPPDATA%\FocusLens unless FOCUSLENS_DATA_DIR overrides it.
/// </summary>
public static class AppPaths
{
    public static string Root
    {
        get
        {
            var overrideDir = Environment.GetEnvironmentVariable("FOCUSLENS_DATA_DIR");
            var dir = !string.IsNullOrWhiteSpace(overrideDir)
                ? overrideDir
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FocusLens");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string DatabaseFile => Path.Combine(Root, "focuslens.db");
    public static string PrivacyFile => Path.Combine(Root, "privacy.json");
    public static string TrackingFile => Path.Combine(Root, "tracking.json");
    public static string SharedStateFile => Path.Combine(Root, "shared-state.json");
    public static string AiSettingsFile => Path.Combine(Root, "ai-settings.json");
    public static string AppSettingsFile => Path.Combine(Root, "app-settings.json");
    /// <summary>Persistent ChromaDB folder. Not created here: its absence means "no index yet".</summary>
    public static string ChromaDir => Path.Combine(Root, "chroma");
    /// <summary>Private Python environment that FocusLens creates for ChromaDB.</summary>
    public static string ChromaVenvDir => Path.Combine(Root, "chroma-venv");
    public static string ChromaPython => Path.Combine(ChromaVenvDir, "Scripts", "python.exe");
    public static string LogsDir => Ensure(Path.Combine(Root, "logs"));
    public static string MeetingsDir => Ensure(Path.Combine(Root, "meetings"));

    public static string MeetingDir(string meetingId) => Ensure(Path.Combine(MeetingsDir, meetingId));

    private static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
