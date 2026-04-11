using System;
using System.Collections.Generic;
using SimSharp;

namespace simSharpSimulation
{
    internal static class ArztPhase
    {
        // --- ARZT (DOCTOR) PHASE ---
        internal static IEnumerable<Event> DurchlaufeArzt(
            Simulation env,
            int patientId,
            Resource arzt,
            double ankunftszeit,
            Random rnd,
            SimulationsDaten daten)
        {
            using (var arztAnfrage = arzt.Request())
            {
                // EREIGNIS 6: Patient geht zur Arzt-Warteschlange
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_zu_arzt_warteschlange", patientId);
                yield return arztAnfrage;

                double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                double arztWartezeit = nowMinutes - ankunftszeit;
                daten.Wartezeiten.Add(arztWartezeit);

                // EREIGNIS 7: Patient verlässt die Arzt-Warteschlange
                daten.LogEvent(nowMinutes, "verlaesst_arzt_warteschlange", patientId);
                // EREIGNIS 8: Patient geht zum Arzt
                daten.LogEvent(nowMinutes, "geht_zu_arzt", patientId);

                double arztBehandlungsdauer = MathNet.Numerics.Distributions.Exponential.Sample(
                    rnd,
                    1.0 / ArztKonfiguration.MITTLERE_BEHANDLUNGSZEIT);
                // Behandlungsdauer wird als Exponentialverteilung modelliert,
                // da sie oft für Wartezeiten und Servicezeiten in Warteschlangensystemen verwendet wird.
                // Lambda (Rate) = 1 / der mittleren Behandlungszeit,
                // ..rechnet der Code: 1.0 / 5.0 = 0.2 Das bedeutet: Der Arzt schafft durchschnittlich 0,2 Patienten pro Minute

                yield return env.Timeout(TimeSpan.FromMinutes(arztBehandlungsdauer));

                // EREIGNIS 9: Patient verlässt den Arzt
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "verlaesst_arzt", patientId);
            }
        }
    }
}
