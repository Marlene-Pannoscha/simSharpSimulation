using System;
using System.Collections.Generic;
using SimSharp;

namespace simSharpSimulation
{
    internal static class SchwesterPhase
    {
        // --- SCHWESTER (NURSE) PHASE ---
        internal static IEnumerable<Event> DurchlaufeSchwester(
            Simulation env,
            int patientId,
            Resource schwester,
            double ankunftszeit,
            bool direktZurSchwester,
            bool pruefeVorbereitungNachZimmer,
            double wahrscheinlichkeitVorbereitung,
            Random rnd,
            SimulationsDaten daten)
        {
            using (var schwesterAnfrage = schwester.Request())
            {
                if (!direktZurSchwester)
                {
                    // EREIGNIS 4: Patient geht zur Schwester-Warteschlange
                    daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_zu_schwester_warteschlange", patientId);
                }

                yield return schwesterAnfrage;

                double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                double schwesterWartezeit = nowMinutes - ankunftszeit;
                daten.SchwesternWartezeiten.Add(schwesterWartezeit);

                if (!direktZurSchwester)
                {
                    daten.LogEvent(nowMinutes, "verlaesst_schwester_warteschlange", patientId);
                }

                // EREIGNIS 4: Patient geht zur Schwester
                daten.LogEvent(nowMinutes, "geht_zur_schwester", patientId);

                if (pruefeVorbereitungNachZimmer)
                {
                    bool brauchtVorbereitungNachZimmer = rnd.NextDouble() < wahrscheinlichkeitVorbereitung;
                    if (brauchtVorbereitungNachZimmer)
                    {
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "braucht_vorbereitung_nach_schwesterzimmer", patientId);

                        double schwesternBehandlungsdauer = MathNet.Numerics.Distributions.Exponential.Sample(
                            rnd,
                            1.0 / SchwesterKonfiguration.MITTLERE_SCHWESTER_ZEIT);
                        yield return env.Timeout(TimeSpan.FromMinutes(schwesternBehandlungsdauer));

                        nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                        daten.LogEvent(nowMinutes, "verlaesst_schwester", patientId);
                    }
                    else
                    {
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_ohne_vorbereitung_zum_arzt", patientId);
                    }
                }
                else
                {
                    double schwesternBehandlungsdauer = MathNet.Numerics.Distributions.Exponential.Sample(
                        rnd,
                        1.0 / SchwesterKonfiguration.MITTLERE_SCHWESTER_ZEIT);

                    yield return env.Timeout(TimeSpan.FromMinutes(schwesternBehandlungsdauer));

                    // EREIGNIS 5: Patient verlässt die Schwester
                    nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                    daten.LogEvent(nowMinutes, "verlaesst_schwester", patientId);
                }
            }
        }
    }
}
