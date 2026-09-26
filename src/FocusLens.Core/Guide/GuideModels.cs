namespace FocusLens.Core.Guide;

/// <summary>What the user is expected to do in a step. Mirrors GuideAction on the Mac.</summary>
public enum GuideAction { Click, Toggle, Type, Open, Look }

/// <summary>
/// Where on screen a step points. Resolved to a real element when the step starts, never stored
/// as coordinates, because windows move.
/// </summary>
public sealed record GuideTarget(string Label, string? Role = null, string? Area = null);

public sealed record GuideStep(
    string Title,
    string? Detail,
    GuideAction Action,
    GuideTarget? Target,
    string? Text = null)
{
    public Guid Id { get; } = Guid.NewGuid();
}

/// <summary>A rectangle in screen pixels, top-left origin. Plain data so Core needs no WPF.</summary>
public readonly record struct GuideRect(double X, double Y, double Width, double Height)
{
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
    public double Area => Width * Height;

    public bool Contains(double x, double y, double margin = 0) =>
        x >= X - margin && x <= X + Width + margin && y >= Y - margin && y <= Y + Height + margin;

    public bool MovedFrom(GuideRect other, double tolerance = 4) =>
        Math.Abs(CenterX - other.CenterX) > tolerance || Math.Abs(CenterY - other.CenterY) > tolerance;
}

/// <summary>One thing on screen that UI Automation reported.</summary>
public sealed record GuideElement(
    string Role, string Label, GuideRect Frame, string AppName, string? State = null, string? Window = null,
    bool OnPage = false);

/// <summary>
/// What to do next, given the screen the model was just shown. A guide keeps asking for this as the
/// screen changes, instead of following one script.
/// </summary>
public enum GuidePlanStatus { Proceed, Done, Blocked }

public sealed record GuidePlan(GuidePlanStatus Status, IReadOnlyList<GuideStep> Steps, string? Note);

/// <summary>Where a guide run is.</summary>
public enum GuidePhaseKind { Idle, Planning, Guiding, Finished, Failed }

public sealed record GuidePhase(GuidePhaseKind Kind, int Index = 0, int Total = 0, string? Message = null)
{
    public static GuidePhase Idle { get; } = new(GuidePhaseKind.Idle);
    public bool IsRunning => Kind is GuidePhaseKind.Planning or GuidePhaseKind.Guiding;
}
