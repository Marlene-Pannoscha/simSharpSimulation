using SimSharp;
using System;
using System.Collections.Generic;

namespace simSharpSimulation
{
    /*
     * Diese Klasse kapselt die Logik fuer die Schwester-Phase im Simulationsprozess.
     * Die Warteschlange ist analog zur Arzt-Phase aufgebaut:
     * warten auf freie Ressource oder Abbruch wegen Wartezeit/Feierabend.
     */
    public static class SchwesterPhase
    {
        private static int GetPriority(PatientenTyp typ)
        {
            return typ switch
            {
                PatientenTyp.Kurz => 3,
                PatientenTyp.Mittel => 2,
                PatientenTyp.Lang => 1,
                _ => 1
            };
        }

        public static IEnumerable<Event> DurchlaufeSchwester(
            Simulation env,
            int patientId,
            PriorityResource schwester,
            int schwesterId,
            PatientenTyp patientenTyp,
            double ankunftszeit,
            bool hatTermin,
            bool direktZurSchwester,
            TimeSpan interneBewegungsdauer,
            double behandlungsdauer,
            SimulationsDaten daten,
            BehandlungsPhaseErgebnis ergebnis)
        {
            double schichtEndeMinuten = SimulationKonfiguration.SIMULATIONSDAUER;
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;

            if (!direktZurSchwester)
            {
                daten.LogEvent(nowMinutes, "betritt_schwester_warteschlange", patientId);
            }

            if (nowMinutes >= schichtEndeMinuten)
            {
                foreach (Event ev in BrichSchwesterWartenAb(env, daten, patientId, schwesterId, interneBewegungsdauer, ergebnis))
                    yield return ev;
                yield break;
            }

            while (schwester.Remaining <= 0)
            {
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                double restMinuten = schichtEndeMinuten - nowMinutes;
                if (restMinuten <= 0)
                {
                    foreach (Event ev in BrichSchwesterWartenAb(env, daten, patientId, schwesterId, interneBewegungsdauer, ergebnis))
                        yield return ev;
                    yield break;
                }

                Event schwesterVerfuegbar = schwester.WhenAny();
                Event schichtEnde = env.Timeout(TimeSpan.FromMinutes(restMinuten));
                yield return schwesterVerfuegbar | schichtEnde;

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                if (!schwesterVerfuegbar.IsProcessed)
                {
                    foreach (Event ev in BrichSchwesterWartenAb(env, daten, patientId, schwesterId, interneBewegungsdauer, ergebnis))
                        yield return ev;
                    yield break;
                }
            }

            using (Request req = schwester.Request(priority: GetPriority(patientenTyp)))
            {
                // Request abgeschlossen: Patient hat die Schwester zugewiesen bekommen.
                // Behandlung darf auch nach Schichtende zu Ende gefuehrt werden.
                yield return req;

                if (!direktZurSchwester)
                {
                    daten.LogEvent(nowMinutes, "verlaesst_wartezimmer", patientId);
                }

                daten.LogEvent(nowMinutes, "geht_zur_schwester", patientId);
                yield return env.Timeout(interneBewegungsdauer);

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "betritt_schwesterzimmer", patientId);
                daten.LogEvent(nowMinutes, "startet_schwester_prozess", patientId, schwesterId: schwesterId);

                double wartezeitSchwester = nowMinutes - ankunftszeit;
                daten.ErfasseSchwesterWartezeit(wartezeitSchwester, patientenTyp, hatTermin);

                daten.ErfasseSchwesterBehandlungszeit(behandlungsdauer, hatTermin, patientenTyp);
                yield return env.Timeout(TimeSpan.FromMinutes(behandlungsdauer));

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "beendet_schwester_prozess", patientId, schwesterId: schwesterId);
            }
        }

        private static IEnumerable<Event> BrichSchwesterWartenAb(
            Simulation env,
            SimulationsDaten daten,
            int patientId,
            int schwesterId,
            TimeSpan interneBewegungsdauer,
            BehandlungsPhaseErgebnis ergebnis)
        {
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            // Hit/Miss jetzt nur noch: Abbruch wegen Feierabend.
            daten.ErfasseSchwesterAbbruchFeierabend(env.StartDate);
            daten.LogEvent(nowMinutes, "bricht_ab_wegen_feierabend_schwester", patientId, schwesterId: schwesterId);

            daten.LogEvent(nowMinutes, "geht_zum_ausgang", patientId);
            yield return env.Timeout(interneBewegungsdauer);

            nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.LogEvent(nowMinutes, "verlaesst_klinik", patientId);
            daten.SchliessePrognosen(patientId, nowMinutes);
            ergebnis.MarkiereKlinikVerlassen();
        }
    }
}
