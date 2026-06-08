using SimSharp;
using System;
using System.Collections.Generic;

namespace simSharpSimulation
{
    public static class SchwesterPhase
    {
        private static int GetPriority(PatientenTyp typ)
        {
            return typ switch
            {
                PatientenTyp.Kurz => 1,
                PatientenTyp.Mittel => 2,
                PatientenTyp.Lang => 3,
                _ => 3
            };
        }

        public static IEnumerable<Event> DurchlaufeSchwester(
            Simulation env,
            int patientId,
            BeweglicherSchwesterPool schwestern,
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

            bool schwesterSofortVerfuegbar = schwestern.IstFrei;
            bool betrittWarteschlange = !direktZurSchwester || !schwesterSofortVerfuegbar;

            if (betrittWarteschlange)
            {
                daten.LogEvent(nowMinutes, "betritt_schwester_warteschlange", patientId);
            }

            if (nowMinutes >= schichtEndeMinuten)
            {
                foreach (Event ev in BrichSchwesterWartenAb(env, daten, patientId, null, interneBewegungsdauer, ergebnis))
                    yield return ev;
                yield break;
            }

            while (!schwestern.IstFrei)
            {
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                double restMinuten = schichtEndeMinuten - nowMinutes;
                if (restMinuten <= 0)
                {
                    foreach (Event ev in BrichSchwesterWartenAb(env, daten, patientId, null, interneBewegungsdauer, ergebnis))
                        yield return ev;
                    yield break;
                }

                Event schwesterFrei = schwestern.WennMitarbeiterFreiWird();
                Event schichtEnde = env.Timeout(TimeSpan.FromMinutes(restMinuten));
                yield return schwesterFrei | schichtEnde;

                if (!schwesterFrei.IsProcessed)
                {
                    foreach (Event ev in BrichSchwesterWartenAb(env, daten, patientId, null, interneBewegungsdauer, ergebnis))
                        yield return ev;
                    yield break;
                }
            }

            using (Request req = schwestern.FordereMitarbeiterAn(GetPriority(patientenTyp)))
            {
                yield return req;

                int schwesterId = schwestern.UebernehmeFreienMitarbeiter();

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                if (betrittWarteschlange)
                {
                    daten.LogEvent(nowMinutes, "verlaesst_wartezimmer_schwester", patientId);
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
                schwestern.GibMitarbeiterZurueck(schwesterId);
            }
        }

        private static IEnumerable<Event> BrichSchwesterWartenAb(
            Simulation env,
            SimulationsDaten daten,
            int patientId,
            int? schwesterId,
            TimeSpan interneBewegungsdauer,
            BehandlungsPhaseErgebnis ergebnis)
        {
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
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
