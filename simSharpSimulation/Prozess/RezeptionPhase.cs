using SimSharp;
using System;
using System.Collections.Generic;

namespace simSharpSimulation
{
    /*
     * Diese Klasse kapselt die Logik fuer die Rezeptions-Phase.
     * Die Warteschlange ist analog zu Arzt und Schwester begrenzt:
     * warten auf freie Ressource oder Abbruch wegen Wartezeit/Feierabend.
     */
    public static class RezeptionPhase
    {
        public static IEnumerable<Event> DurchlaufeRezeption(
            Simulation env,
            int patientId,
            Resource rezeption,
            double ankunftszeit,
            bool hatTermin,
            bool behandlungBereitsFertig,
            TimeSpan wegZumAusgang,
            double behandlungsdauer,
            SimulationsDaten daten,
            BehandlungsPhaseErgebnis ergebnis,
            PrognoseRessourcenStatus prognoseStatus)
        {
            double schichtEndeMinuten = SimulationKonfiguration.SIMULATIONSDAUER;
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;

            daten.LogEvent(nowMinutes, "betritt_rezeption_warteschlange", patientId);
            prognoseStatus.RegistriereWartend(patientId, behandlungsdauer);

            bool rezeptionWarFrei = IstRezeptionFrei(rezeption);
            daten.LogEvent(nowMinutes, rezeptionWarFrei ? "rezeption_frei" : "rezeption_nicht_frei", patientId);
            if (!rezeptionWarFrei)
            {
                daten.LogEvent(nowMinutes, "wartet_in_rezeption_warteschlange", patientId);
            }

            if (nowMinutes >= schichtEndeMinuten)
            {
                foreach (Event ev in BrichRezeptionWartenAb(env, daten, patientId, wegZumAusgang, ergebnis, prognoseStatus))
                    yield return ev;
                yield break;
            }

            bool brichtWartenAb = false;
            bool behandlungGestartet = false;
            Request req = rezeption.Request();
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
                behandlungGestartet = true;

                if (!rezeptionWarFrei)
                {
                    daten.LogEvent(nowMinutes, "rezeption_frei", patientId);
                }

                daten.LogEvent(nowMinutes, "betritt_rezeption", patientId);
                daten.LogEvent(nowMinutes, behandlungBereitsFertig ? "behandlung_bereits_fertig" : "behandlung_nicht_fertig", patientId);
                daten.LogEvent(nowMinutes, "startet_rezeption", patientId);
                prognoseStatus.StarteBehandlung(patientId, nowMinutes, behandlungsdauer);

                double wartezeitRezeption = nowMinutes - ankunftszeit;
                daten.ErfasseRezeptionWartezeit(wartezeitRezeption, hatTermin);

                daten.ErfasseRezeptionBehandlungszeit(behandlungsdauer, hatTermin);

                yield return env.Timeout(TimeSpan.FromMinutes(behandlungsdauer));

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "beendet_rezeption", patientId);
                prognoseStatus.BeendeBehandlung(patientId);
                if (behandlungBereitsFertig)
                {
                    daten.LogEvent(nowMinutes, "macht_folgetermin_aus_oder_rezept", patientId);
                }
                else
                {
                    daten.LogEvent(nowMinutes, hatTermin ? "rezeption_hat_termin" : "rezeption_ohne_termin", patientId);
                }
            }

            if (behandlungGestartet)
            {
                req.Dispose();
            }

            if (brichtWartenAb)
            {
                foreach (Event ev in BrichRezeptionWartenAb(env, daten, patientId, wegZumAusgang, ergebnis, prognoseStatus))
                    yield return ev;
            }
        }

        private static IEnumerable<Event> BrichRezeptionWartenAb(
            Simulation env,
            SimulationsDaten daten,
            int patientId,
            TimeSpan interneBewegungsdauer,
            BehandlungsPhaseErgebnis ergebnis,
            PrognoseRessourcenStatus prognoseStatus)
        {
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            prognoseStatus.EntfernePatient(patientId);
            // Hit/Miss jetzt nur noch: Abbruch wegen Feierabend.
            daten.ErfasseRezeptionAbbruchFeierabend(env.StartDate);
            daten.LogEvent(nowMinutes, "bricht_ab_wegen_feierabend_rezeption", patientId);

            daten.LogEvent(nowMinutes, "geht_zum_ausgang", patientId);
            yield return env.Timeout(interneBewegungsdauer);

            nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.LogEvent(nowMinutes, "verlaesst_klinik", patientId);
            daten.SchliessePrognosen(patientId, nowMinutes);
            ergebnis.MarkiereKlinikVerlassen();
        }

        private static bool IstRezeptionFrei(Resource rezeption)
        {
            return rezeption.Remaining > 0;
        }
    }
}
