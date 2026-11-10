using SimSharp;
using static simSharpSimulation.SimulationsHelfer;
using System;
using System.Collections.Generic;

namespace simSharpSimulation
{
    /*
     * Diese Klasse kapselt die Logik fuer die Rezeptions-Phase.
     * Patienten warten auf eine freie Ressource oder brechen wegen Feierabend ab.
     * Eine begonnene Behandlung wird auch nach Schichtende abgeschlossen.
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
            double jetztMinuten = AktuelleSimulationszeitMinuten(env);

            daten.LogEvent(jetztMinuten, "betritt_rezeption_warteschlange", patientId);
            prognoseStatus.RegistriereWartend(patientId, behandlungsdauer);

            bool rezeptionWarFrei = IstRezeptionFrei(rezeption);
            daten.LogEvent(jetztMinuten, rezeptionWarFrei ? "rezeption_frei" : "rezeption_nicht_frei", patientId);
            if (!rezeptionWarFrei)
            {
                daten.LogEvent(jetztMinuten, "wartet_in_rezeption_warteschlange", patientId);
            }

            if (jetztMinuten >= schichtEndeMinuten)
            {
                foreach (Event ev in BrichRezeptionWartenAb(env, daten, patientId, wegZumAusgang, ergebnis, prognoseStatus))
                    yield return ev;
                yield break;
            }

            bool brichtWartenAb = false;
            using (Request rezeptionsAnforderung = rezeption.Request())
            {
                yield return WarteAufRessourceOderSchichtende(env, rezeptionsAnforderung, schichtEndeMinuten);

                if (MussWartenAbbrechen(env, rezeptionsAnforderung, schichtEndeMinuten))
                {
                    brichtWartenAb = true;
                }
                else
                {
                    foreach (Event ev in FuehreRezeptionBehandlungDurch(
                        env, patientId, ankunftszeit, hatTermin, behandlungBereitsFertig,
                        rezeptionWarFrei, behandlungsdauer, daten, prognoseStatus))
                    {
                        yield return ev;
                    }
                }
            }

            if (brichtWartenAb)
            {
                foreach (Event ev in BrichRezeptionWartenAb(env, daten, patientId, wegZumAusgang, ergebnis, prognoseStatus))
                    yield return ev;
            }
        }

        private static IEnumerable<Event> FuehreRezeptionBehandlungDurch(
            Simulation env,
            int patientId,
            double ankunftszeit,
            bool hatTermin,
            bool behandlungBereitsFertig,
            bool rezeptionWarFrei,
            double behandlungsdauer,
            SimulationsDaten daten,
            PrognoseRessourcenStatus prognoseStatus)
        {
            double jetztMinuten = AktuelleSimulationszeitMinuten(env);
            if (!rezeptionWarFrei)
            {
                daten.LogEvent(jetztMinuten, "rezeption_frei", patientId);
            }

            daten.LogEvent(jetztMinuten, "betritt_rezeption", patientId);
            daten.LogEvent(jetztMinuten, behandlungBereitsFertig ? "behandlung_bereits_fertig" : "behandlung_nicht_fertig", patientId);
            daten.LogEvent(jetztMinuten, "startet_rezeption", patientId);
            prognoseStatus.StarteBehandlung(patientId, jetztMinuten, behandlungsdauer);

            double wartezeitRezeption = jetztMinuten - ankunftszeit;
            daten.ErfasseRezeptionWartezeit(wartezeitRezeption, hatTermin);

            daten.ErfasseRezeptionBehandlungszeit(behandlungsdauer, hatTermin);

            // Eine begonnene Behandlung laeuft bis zum Ende; die Schichtgrenze wird nur beim Warten geprueft.
            yield return env.Timeout(TimeSpan.FromMinutes(behandlungsdauer));

            jetztMinuten = AktuelleSimulationszeitMinuten(env);
            daten.LogEvent(jetztMinuten, "beendet_rezeption", patientId);
            prognoseStatus.BeendeBehandlung(patientId);
            if (behandlungBereitsFertig)
            {
                daten.LogEvent(jetztMinuten, "macht_folgetermin_aus_oder_rezept", patientId);
            }
            else
            {
                daten.LogEvent(jetztMinuten, hatTermin ? "rezeption_hat_termin" : "rezeption_ohne_termin", patientId);
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
            double jetztMinuten = AktuelleSimulationszeitMinuten(env);
            prognoseStatus.EntfernePatient(patientId);
            // Hit/Miss jetzt nur noch: Abbruch wegen Feierabend.
            daten.ErfasseRezeptionAbbruchFeierabend(env.StartDate);
            daten.LogEvent(jetztMinuten, "bricht_ab_wegen_feierabend_rezeption", patientId);

            daten.LogEvent(jetztMinuten, "geht_zum_ausgang", patientId, bewegungsdauerMinuten: interneBewegungsdauer.TotalMinutes);
            yield return env.Timeout(interneBewegungsdauer);

            jetztMinuten = AktuelleSimulationszeitMinuten(env);
            daten.LogEvent(jetztMinuten, "verlaesst_klinik", patientId);
            daten.SchliessePrognosen(patientId, jetztMinuten);
            ergebnis.MarkiereKlinikVerlassen();
        }

        private static bool IstRezeptionFrei(Resource rezeption)
        {
            return rezeption.Remaining > 0;
        }
    }
}
