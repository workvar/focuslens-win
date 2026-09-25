using CommunityToolkit.Mvvm.ComponentModel;
using FocusLens.Core.Focus;

namespace FocusLens.App.ViewModels.Focus;

/// <summary>One chip in the "For how long?" row.</summary>
public sealed partial class FocusDurationOption : ObservableObject
{
    [ObservableProperty] private bool _isSelected;

    public FocusDurationOption(int minutes, string label)
    {
        Minutes = minutes;
        Label = label;
    }

    public int Minutes { get; }
    public string Label { get; }

    public static List<FocusDurationOption> All() => new()
    {
        new(15, "15 min"), new(25, "25 min"), new(45, "45 min"),
        new(60, "1 hour"), new(90, "1.5 hours"), new(120, "2 hours"),
    };
}

/// <summary>One of the two big "When you drift" choices.</summary>
public sealed partial class FocusEnforcementOption : ObservableObject
{
    [ObservableProperty] private bool _isSelected;

    public FocusEnforcementOption(FocusEnforcement mode) => Mode = mode;

    public FocusEnforcement Mode { get; }
    public string Title => Mode.Title();
    public string Summary => Mode.Summary();
    /// <summary>Cancel for close, Lock for block (Segoe MDL2 Assets).</summary>
    public string Glyph => Mode == FocusEnforcement.Block ? "" : "";
}
