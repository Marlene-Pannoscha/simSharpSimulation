using System.Diagnostics;
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
        double simulatedSecondsPerSecond, CancellationToken cancellationToken)
    {
        if (!double.IsFinite(simulatedSecondsPerSecond) || simulatedSecondsPerSecond is < 1 or > 600)
            throw new ArgumentOutOfRangeException(nameof(simulatedSecondsPerSecond));

        _ = Configuration.Value;
        var daten = new SimulationsDaten();
        daten.LiveEventOccurred += publish;
        var prozess = new PatientenProzess(SimulationKonfiguration.RANDOM_SEED, daten);
        await prozess.FuehreAusAsync(1, async (env, dauer) =>
        {
            var clock = Stopwatch.StartNew();
            var end = env.StartDate + dauer;
            while (env.Peek() < end)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var due = (env.Peek() - env.StartDate).TotalSeconds / simulatedSecondsPerSecond;
                var remaining = due - clock.Elapsed.TotalSeconds;
                if (remaining > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(remaining, 0.1)), cancellationToken);
                    continue;
                }
                env.Step();
            }
        });
    }
}
