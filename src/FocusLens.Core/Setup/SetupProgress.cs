namespace FocusLens.Core.Setup;

/// <summary>Progress of an install step. Fraction is 0..1, or negative when the length is unknown.</summary>
public readonly record struct SetupProgress(double Fraction, string Message);

/// <summary>Thrown for expected setup failures whose message is safe to show to the user as-is.</summary>
public sealed class SetupException : Exception
{
    public SetupException(string message, Exception? inner = null) : base(message, inner) { }
}
