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
            BehandlungsPhaseErgebnis ergebnis,
            PrognoseRessourcenStatus prognoseStatus)
        {
            double schichtEndeMinuten = SimulationKonfiguration.SIMULATIONSDAUER;
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;

            bool schwesterSofortVerfuegbar = schwestern.IstFrei;
            bool betrittWarteschlange = !direktZurSchwester || !schwesterSofortVerfuegbar;
            int prioritaet = GetPriority(patientenTyp);
            prognoseStatus.RegistriereWartend(patientId, behandlungsdauer, prioritaet);

            if (betrittWarteschlange)
            {
                daten.LogEvent(nowMinutes, "betritt_schwester_warteschlange", patientId);
            }

            if (nowMinutes >= schichtEndeMinuten)
            {
                foreach (Event ev in BrichSchwesterWartenAb(env, daten, patientId, null, interneBewegungsdauer, ergebnis, prognoseStatus))
                    yield return ev;
                yield break;
            }

            bool brichtWartenAb = false;
            using (Request req = schwestern.FordereMitarbeiterAn(prioritaet))
            {
                double restMinuten = schichtEndeMinuten - (env.Now - env.StartDate).TotalMinutes;
                Event schichtEnde = env.Timeout(TimeSpan.FromMinutes(restMinuten));
                yield return req | schichtEnde;

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                if (!req.IsProcessed || nowMinutes >= schichtEndeMinuten)
                {
                    brichtWartenAb = true;
                }
                else
                {
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
                    prognoseStatus.StarteBehandlung(patientId, nowMinutes, behandlungsdauer);

                    double wartezeitSchwester = nowMinutes - ankunftszeit;
                    daten.ErfasseSchwesterWartezeit(wartezeitSchwester, patientenTyp, hatTermin);

                    daten.ErfasseSchwesterBehandlungszeit(behandlungsdauer, hatTermin, patientenTyp);
                    yield return env.Timeout(TimeSpan.FromMinutes(behandlungsdauer));

                    nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                    daten.LogEvent(nowMinutes, "beendet_schwester_prozess", patientId, schwesterId: schwesterId);
                    prognoseStatus.BeendeBehandlung(patientId);
                    schwestern.GibMitarbeiterZurueck(schwesterId);
                }
            }

            if (brichtWartenAb)
            {
                foreach (Event ev in BrichSchwesterWartenAb(env, daten, patientId, null, interneBewegungsdauer, ergebnis, prognoseStatus))
                    yield return ev;
            }
        }

        private static IEnumerable<Event> BrichSchwesterWartenAb(
            Simulation env,
            SimulationsDaten daten,
            int patientId,
            int? schwesterId,
            TimeSpan interneBewegungsdauer,
            BehandlungsPhaseErgebnis ergebnis,
            PrognoseRessourcenStatus prognoseStatus)
        {
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            prognoseStatus.EntfernePatient(patientId);
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
