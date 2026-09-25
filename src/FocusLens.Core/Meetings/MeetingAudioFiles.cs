using FocusLens.Core.Paths;

namespace FocusLens.Core.Meetings;

/// <summary>Removes a meeting's saved audio from disk. Only ever touches folders under the meetings directory.</summary>
public static class MeetingAudioFiles
{
    /// <summary>Returns true when the folder is gone (or never existed). Never throws.</summary>
    public static bool TryDelete(string? audioDir)
    {
        if (string.IsNullOrWhiteSpace(audioDir)) return true;
        try
        {
            var root = Path.GetFullPath(AppPaths.MeetingsDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var target = Path.GetFullPath(audioDir);
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return false; // refuse anything outside our data folder
            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
