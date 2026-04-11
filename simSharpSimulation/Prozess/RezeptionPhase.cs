using System;
using System.Collections.Generic;
using SimSharp;

namespace simSharpSimulation
{
    internal static class RezeptionPhase
    {
        // --- REZEPTION (RECEPTION) PHASE ---
        internal static IEnumerable<Event> DurchlaufeRezeption(
            Simulation env,
            int patientId,
            Resource rezeption,
            double ankunftszeit,
            Random rnd,
            SimulationsDaten daten)
        {
            using (var rezeptionAnfrage = rezeption.Request())
            {
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_zur_rezeption_warteschlange", patientId);
                yield return rezeptionAnfrage;

                double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                double rezeptionWartezeit = nowMinutes - ankunftszeit;
                daten.RezeptionsWartezeiten.Add(rezeptionWartezeit);

                daten.LogEvent(nowMinutes, "verlaesst_rezeption_warteschlange", patientId);
                daten.LogEvent(nowMinutes, "geht_zur_rezeption", patientId);

                double rezeptionServiceDauer = MathNet.Numerics.Distributions.Exponential.Sample(
                    rnd,
                    1.0 / RezeptionKonfiguration.MITTELREZEPTIONSZEIT);

                yield return env.Timeout(TimeSpan.FromMinutes(rezeptionServiceDauer));

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "verlaesst_rezeption", patientId);
            }
        }
    }
}
