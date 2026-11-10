using System.Diagnostics;

namespace PraxisSimulation.Core;

/// <summary>Eine stetige Zeitbasis pro Lauf; Geschwindigkeitswechsel erhalten die erreichte Zeit.</summary>
public sealed class SimulationClock(double speed)
{
    private readonly object gate = new();
    private readonly Stopwatch timer = new();
    private double seconds;
    private double factor = speed;
    public double Speed { get { lock (gate) return factor; } }
    public double Seconds { get { lock (gate) return seconds + timer.Elapsed.TotalSeconds * factor; } }
    public void Start() { lock (gate) timer.Start(); }
    public void Stop() { lock (gate) { seconds += timer.Elapsed.TotalSeconds * factor; timer.Reset(); } }
    public void SetSpeed(double value)
    {
        if (!double.IsFinite(value) || value is < 0.5 or > 600) throw new ArgumentOutOfRangeException(nameof(value));
        lock (gate)
        {
            seconds += timer.Elapsed.TotalSeconds * factor;
            var running = timer.IsRunning;
            timer.Reset();
            factor = value;
            if (running) timer.Start();
        }
    }
}
