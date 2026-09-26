namespace FocusLens.Core.Guide;

/// <summary>
/// What machine this is. Read once at the start of a guide and put at the top of every prompt
/// after that.
///
/// Without it the model was working from what it knows about computers in general, which is where
/// invented controls come from: it would name a Control Panel page that Settings replaced, a menu a
/// Windows release moved, or an app the user does not have. Guide would then report that the
/// controls it was given are not on this screen, which is true but unhelpful, because the real
/// problem was that nobody had told the model which computer it was looking at.
///
/// The interface language matters most of all. On a machine set to anything but English every
/// control is labelled in that language, and an English guess can never match.
/// </summary>
public sealed record GuideSystem(
    string Os,
    string? Device = null,
    string? Language = null,
    string? DefaultBrowser = null,
    IReadOnlyList<string>? Apps = null)
{
    /// <summary>
    /// Enough to cover what anyone has pinned and installed without crowding out the screen list,
    /// which matters more.
    /// </summary>
    public const int MaxApps = 60;

    public IReadOnlyList<string> Apps { get; init; } = Apps ?? Array.Empty<string>();

    /// <summary>Used when the machine could not be read. The guide still runs; it just has one fewer thing to go on.</summary>
    public static GuideSystem Unknown(string os) => new(os);
}
