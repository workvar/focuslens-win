using FocusLens.Platform.Windows.Native;

namespace FocusLens.Platform.Windows.Capture;

/// <summary>
/// Detects that the clipboard changed (a sequence-number check, never reading its content) and
/// whether the owner marked the copy as sensitive, as password managers do.
/// </summary>
public sealed class ClipboardWatcher
{
    private static readonly uint[] HiddenFormats =
    {
        User32.RegisterClipboardFormatW("ExcludeClipboardContentFromMonitorProcessing"),
        User32.RegisterClipboardFormatW("Clipboard Viewer Ignore"),
    };

    private uint _last = User32.GetClipboardSequenceNumber();

    /// <summary>True once per new copy; false if nothing changed or the copy is flagged as hidden.</summary>
    public bool PollForCopy()
    {
        var current = User32.GetClipboardSequenceNumber();
        if (current == _last) return false;
        _last = current;

        // Either marker means the owner asked clipboard tools to ignore this copy.
        return !(User32.IsClipboardFormatAvailable(HiddenFormats[0]) || User32.IsClipboardFormatAvailable(HiddenFormats[1]));
    }
}
