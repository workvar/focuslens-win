using System.Reflection;
using FocusLens.Core.Paths;

namespace FocusLens.Core.Chroma;

/// <summary>
/// The ingest and search scripts ship inside the Core assembly. They are copied to the data folder before each run,
/// so an app update also updates them and Python never has to read from the install folder.
/// </summary>
public static class ChromaScripts
{
    private const string Prefix = "chroma/";

    public static string PathOf(string name) => Path.Combine(AppPaths.ChromaScriptsDir, name);

    /// <summary>Returns false when no scripts are embedded, which means a broken build rather than a user problem.</summary>
    public static bool Stage()
    {
        var assembly = typeof(ChromaScripts).Assembly;
        var names = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal)).ToList();
        if (names.Count == 0) return false;

        try
        {
            Directory.CreateDirectory(AppPaths.ChromaScriptsDir);
            foreach (var name in names)
            {
                using var source = assembly.GetManifestResourceStream(name)!;
                using var target = File.Create(PathOf(name[Prefix.Length..]));
                source.CopyTo(target);
            }
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
