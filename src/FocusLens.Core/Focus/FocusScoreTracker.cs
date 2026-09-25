namespace FocusLens.Core.Focus;

/// <summary>
/// The live focus score and the running totals behind the final one.
///
/// Live score: starts at 100, sinks toward 0 while the user is off topic (time constant 20 s)
/// and climbs back toward 100 while on topic (45 s), so it reacts within seconds but does not
/// flip on a single glance.
///
/// Final score: focused time as a share of judged time. Neutral time (FocusLens itself, the
/// desktop, a model that could not tell) is left out of both sides.
/// </summary>
public sealed class FocusScoreTracker
{
    public enum Sample
    {
        Focused,
        Distracted,
        Neutral,
    }

    private const double DropTau = 20;
    private const double RecoverTau = 45;
    private const double SampleEvery = 10;

    private readonly List<FocusScorePoint> _timeline = new() { new FocusScorePoint(0, 100) };
    private double _elapsed;
    private double _sinceSample;

    public double Live { get; private set; } = 100;
    public double FocusedSeconds { get; private set; }
    public double DistractedSeconds { get; private set; }
    public double NeutralSeconds { get; private set; }
    public IReadOnlyList<FocusScorePoint> Timeline => _timeline;

    public int LiveScore => (int)Math.Round(Live, MidpointRounding.AwayFromZero);

    public int FinalScore
    {
        get
        {
            var judged = FocusedSeconds + DistractedSeconds;
            return judged > 0 ? (int)Math.Round(FocusedSeconds / judged * 100, MidpointRounding.AwayFromZero) : 100;
        }
    }

    public void Record(Sample sample, double dt)
    {
        if (dt <= 0) return;

        switch (sample)
        {
            case Sample.Focused:
                FocusedSeconds += dt;
                Live += (100 - Live) * (1 - Math.Exp(-dt / RecoverTau));
                break;
            case Sample.Distracted:
                DistractedSeconds += dt;
                Live -= Live * (1 - Math.Exp(-dt / DropTau));
                break;
            default:
                NeutralSeconds += dt;
                break;
        }

        _elapsed += dt;
        _sinceSample += dt;
        if (_sinceSample >= SampleEvery)
        {
            _sinceSample = 0;
            _timeline.Add(new FocusScorePoint(_elapsed, Live));
        }
    }

    /// <summary>Adds the last point so the chart reaches the end of the session.</summary>
    public void CloseTimeline()
    {
        if (_timeline[^1].Offset < _elapsed) _timeline.Add(new FocusScorePoint(_elapsed, Live));
    }
}
