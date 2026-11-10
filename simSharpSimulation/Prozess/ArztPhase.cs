using SimSharp;
using static simSharpSimulation.SimulationsHelfer;
using System;
using System.Collections.Generic;

namespace simSharpSimulation
{
    public static class ArztPhase
    {
        public static IEnumerable<Event> DurchlaufeArzt(
            Simulation env,
            int patientId,
            BeweglicherArztPool aerzte,
            PatientenTyp patientenTyp,
            double ankunftszeit,
            bool hatTermin,
            TimeSpan interneBewegungsdauer,
            double behandlungsdauer,
            Resource arztzimmer,
            SimulationsDaten daten,
            BehandlungsPhaseErgebnis ergebnis,
            PrognoseRessourcenStatus prognoseStatus)
        {
            double schichtEndeMinuten = SimulationKonfiguration.SIMULATIONSDAUER;
            double jetztMinuten = AktuelleSimulationszeitMinuten(env);
            int prioritaet = ErmittlePatientenPrioritaet(patientenTyp);
            prognoseStatus.RegistriereWartend(patientId, behandlungsdauer, prioritaet);

            if (jetztMinuten >= schichtEndeMinuten)
            {
                foreach (Event ev in BrichArztWartenAb(env, daten, patientId, null, interneBewegungsdauer, ergebnis, prognoseStatus))
                    yield return ev;
                yield break;
            }

            // Schritt A3: Der Request wird sofort gestellt, damit die PriorityResource
            // die Reihenfolge nach Prioritaet und FIFO korrekt verwalten kann.
            bool brichtWartenAb = false;
            Request mitarbeiterAnforderung = aerzte.FordereMitarbeiterAn(prioritaet);
            try
            {
                yield return WarteAufRessourceOderSchichtende(env, mitarbeiterAnforderung, schichtEndeMinuten);

                if (MussWartenAbbrechen(env, mitarbeiterAnforderung, schichtEndeMinuten))
                {
                    brichtWartenAb = true;
                }
                else
                {
                    Request raumAnforderung = arztzimmer.Request();
                    try
                    {
                        yield return WarteAufRessourceOderSchichtende(env, raumAnforderung, schichtEndeMinuten);

                        if (MussWartenAbbrechen(env, raumAnforderung, schichtEndeMinuten))
                        {
                            brichtWartenAb = true;
                        }
                    }
                    finally
                    {
                        // Im bestehenden Modell wird der Raum bereits nach der Zuteilung freigegeben,
                        // bevor Bewegung und Behandlung beginnen. Der Mitarbeiter bleibt dagegen gebunden.
                        GibVerarbeiteteAnforderungFrei(raumAnforderung);
                    }

                    if (!brichtWartenAb)
                    {
                        foreach (Event ev in FuehreArztBehandlungDurch(
                            env, patientId, aerzte, patientenTyp, ankunftszeit, hatTermin,
                            interneBewegungsdauer, behandlungsdauer, daten, prognoseStatus))
                        {
                            yield return ev;
                        }
                    }
                }
            }
            finally
            {
                GibVerarbeiteteAnforderungFrei(mitarbeiterAnforderung);
            }

            if (brichtWartenAb)
            {
                foreach (Event ev in BrichArztWartenAb(env, daten, patientId, null, interneBewegungsdauer, ergebnis, prognoseStatus))
                    yield return ev;
            }
        }

        private static IEnumerable<Event> FuehreArztBehandlungDurch(
            Simulation env,
            int patientId,
            BeweglicherArztPool aerzte,
            PatientenTyp patientenTyp,
            double ankunftszeit,
            bool hatTermin,
            TimeSpan interneBewegungsdauer,
            double behandlungsdauer,
            SimulationsDaten daten,
            PrognoseRessourcenStatus prognoseStatus)
        {
            int arztId = aerzte.UebernehmeFreienMitarbeiter();

            // HIT: Ab hier hat der Patient den Arzt tatsaechlich erreicht.
            daten.ErfasseArztBehandlungBegonnen(env.StartDate);

            double jetztMinuten = AktuelleSimulationszeitMinuten(env);
            // Patient verlaesst das Wartezimmer, sobald der Arzt frei ist.
            daten.LogEvent(jetztMinuten, "verlaesst_wartezimmer_fuer_arzt", patientId);

            // Weg zum Arzt (interne Bewegung).
            daten.LogEvent(jetztMinuten, "geht_zum_arzt", patientId, bewegungsdauerMinuten: interneBewegungsdauer.TotalMinutes);
            yield return env.Timeout(interneBewegungsdauer);

            jetztMinuten = AktuelleSimulationszeitMinuten(env);
            daten.LogEvent(jetztMinuten, "betritt_arztzimmer", patientId);

            // Arztbehandlung beginnt.
            daten.LogEvent(jetztMinuten, "startet_arzt_behandlung", patientId, arztId: arztId);
            prognoseStatus.StarteBehandlung(patientId, jetztMinuten, behandlungsdauer);

            // Wartezeit erfassen.
            double wartezeitArzt = jetztMinuten - ankunftszeit;
            daten.ErfasseArztWartezeit(wartezeitArzt, hatTermin, patientenTyp);

            daten.ErfasseArztBehandlungszeit(behandlungsdauer, hatTermin, patientenTyp);

            // Eine begonnene Behandlung laeuft bis zum Ende; die Schichtgrenze wird nur beim Warten geprueft.
            yield return env.Timeout(TimeSpan.FromMinutes(behandlungsdauer));

            jetztMinuten = AktuelleSimulationszeitMinuten(env);
            daten.LogEvent(jetztMinuten, "beendet_arzt_behandlung", patientId, arztId: arztId);
            prognoseStatus.BeendeBehandlung(patientId);
            aerzte.GibMitarbeiterZurueck(arztId);
        }

        private static IEnumerable<Event> BrichArztWartenAb(
            Simulation env,
            SimulationsDaten daten,
            int patientId,
            int? arztId,
            TimeSpan interneBewegungsdauer,
            BehandlungsPhaseErgebnis ergebnis,
            PrognoseRessourcenStatus prognoseStatus)
        {
            double jetztMinuten = AktuelleSimulationszeitMinuten(env);
            prognoseStatus.EntfernePatient(patientId);
            // Hit/Miss jetzt nur noch: Abbruch wegen Feierabend.
            daten.ErfasseArztAbbruchFeierabend(env.StartDate);
            daten.LogEvent(jetztMinuten, "bricht_ab_wegen_feierabend_arzt", patientId, arztId: arztId);

            daten.LogEvent(jetztMinuten, "geht_zum_ausgang", patientId, bewegungsdauerMinuten: interneBewegungsdauer.TotalMinutes);
            yield return env.Timeout(interneBewegungsdauer);

            jetztMinuten = AktuelleSimulationszeitMinuten(env);
            daten.LogEvent(jetztMinuten, "verlaesst_klinik", patientId);
            daten.SchliessePrognosen(patientId, jetztMinuten);
            ergebnis.MarkiereKlinikVerlassen();
        }
    }
}
