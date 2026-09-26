namespace FocusLens.App.Services.Guide;

public enum GuideStepEventKind { Click, ReturnKey, Skip, Tick }

/// <summary>Everything that can end or change a step. Coordinates are screen pixels, for clicks.</summary>
public readonly record struct GuideStepEvent(GuideStepEventKind Kind, int X = 0, int Y = 0);
