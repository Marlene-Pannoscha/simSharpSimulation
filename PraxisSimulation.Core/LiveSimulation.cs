using simSharpSimulation;

namespace PraxisSimulation.Core;

/// <summary>Ein echter, zeitgesteuerter SimSharp-Tag mit unveränderlichen Server-Standardwerten.</summary>
public static class LiveSimulation
{
    private static readonly Lazy<bool> Configuration = new(() =>
    {
        KonfigurationJsonExport.LadeAlle();
        return true;
    });

    public static async Task RunAsync(Action<SimulationLiveEvent> publish,
        double simulatedSecondsPerSecond, CancellationToken cancellationToken,
        SimulationClock? simulationClock = null, Action<double, double>? publishClock = null)
    {
        if (!double.IsFinite(simulatedSecondsPerSecond) || simulatedSecondsPerSecond is < 0.5 or > 600)
            throw new ArgumentOutOfRangeException(nameof(simulatedSecondsPerSecond));

        _ = Configuration.Value;
        var daten = new SimulationsDaten();
        daten.LiveEventOccurred += publish;
        var prozess = new PatientenProzess(SimulationKonfiguration.RANDOM_SEED, daten);
        await prozess.FuehreAusAsync(1, async (env, dauer) =>
        {
            var clock = simulationClock ?? new SimulationClock(simulatedSecondsPerSecond);
            var end = env.StartDate + dauer;
            var lastReport = Environment.TickCount64 - 100;
            double reportedSeconds = 0;
            clock.Start();
            try
            {
                while (env.Peek() < end)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var nextSeconds = (env.Peek() - env.StartDate).TotalSeconds;
                    var target = clock.Seconds;
                    if (nextSeconds <= target)
                    {
                        env.Step();
                        reportedSeconds = (env.Now - env.StartDate).TotalSeconds;
                        continue;
                    }
                    reportedSeconds = Math.Min(target, nextSeconds);
                    if (Environment.TickCount64 - lastReport >= 100)
                    {
                        publishClock?.Invoke(reportedSeconds / 60, clock.Speed);
                        lastReport = Environment.TickCount64;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min((nextSeconds - target) / clock.Speed, 0.025)), cancellationToken);
                }
                reportedSeconds = Math.Min(clock.Seconds, dauer.TotalSeconds);
            }
            finally
            {
                clock.Stop();
                publishClock?.Invoke(reportedSeconds / 60, clock.Speed);
            }
        });
    }
}
