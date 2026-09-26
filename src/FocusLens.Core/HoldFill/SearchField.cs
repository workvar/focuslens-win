using FocusLens.Core.Guide;

namespace FocusLens.Core.HoldFill;

/// <summary>
/// A search field found under the pointer. Only its label, placeholder and whether it is empty
/// are read; its contents never are.
/// </summary>
/// <param name="Id">Stable for the life of the field, so the tracker knows it is still the same one.</param>
/// <param name="Frame">Screen pixels.</param>
/// <param name="AppName">The app that owns the field, such as "Google Chrome".</param>
/// <param name="Label">The field's accessible name or placeholder, such as "Search YouTube".</param>
/// <param name="WindowTitle">Title of the window the field is in, such as the page title.</param>
public sealed record SearchField(
    string Id,
    GuideRect Frame,
    string AppName,
    string Label,
    string WindowTitle,
    bool IsEmpty);
