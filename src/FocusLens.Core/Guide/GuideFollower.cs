namespace FocusLens.Core.Guide;

/// <summary>
/// A point that trails another with a slight delay. Exponential smoothing: each frame closes a
/// fraction of the gap, and the fraction depends on elapsed time, so it looks the same at any
/// refresh rate. Same maths as GuideFollower.swift.
/// </summary>
public sealed class GuideFollower
{
    public double X { get; private set; }
    public double Y { get; private set; }

    /// <summary>Higher is snappier. About 7 trails a few frames behind the pointer.</summary>
    public double Rate { get; set; }

    public GuideFollower(double x, double y, double rate = 10)
    {
        X = x;
        Y = y;
        Rate = rate;
    }

    public void Advance(double targetX, double targetY, double seconds)
    {
        var blend = 1 - Math.Exp(-Rate * Math.Max(seconds, 0));
        X += (targetX - X) * blend;
        Y += (targetY - Y) * blend;
    }

    public bool HasSettled(double targetX, double targetY, double tolerance = 0.5) =>
        Math.Abs(targetX - X) < tolerance && Math.Abs(targetY - Y) < tolerance;
}
