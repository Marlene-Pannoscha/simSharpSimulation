using SimSharp;
using static simSharpSimulation.SimulationsHelfer;
using System;
using System.Collections.Generic;

namespace simSharpSimulation
{
    public static class SchwesterPhase
    {
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
            Resource schwesterzimmer,
            SimulationsDaten daten,
            BehandlungsPhaseErgebnis ergebnis,
            PrognoseRessourcenStatus prognoseStatus)
        {
            double schichtEndeMinuten = SimulationKonfiguration.SIMULATIONSDAUER;
            double jetztMinuten = AktuelleSimulationszeitMinuten(env);

            bool schwesterSofortVerfuegbar = schwestern.IstFrei;
            bool betrittWarteschlange = !direktZurSchwester || !schwesterSofortVerfuegbar;
            int prioritaet = ErmittlePatientenPrioritaet(patientenTyp);
            prognoseStatus.RegistriereWartend(patientId, behandlungsdauer, prioritaet);

            if (betrittWarteschlange)
            {
                daten.LogEvent(jetztMinuten, "betritt_schwester_warteschlange", patientId);
            }

            if (jetztMinuten >= schichtEndeMinuten)
            {
                foreach (Event ev in BrichSchwesterWartenAb(env, daten, patientId, null, interneBewegungsdauer, ergebnis, prognoseStatus))
                    yield return ev;
                yield break;
            }

            bool brichtWartenAb = false;
            Request mitarbeiterAnforderung = schwestern.FordereMitarbeiterAn(prioritaet);
            try
            {
                yield return WarteAufRessourceOderSchichtende(env, mitarbeiterAnforderung, schichtEndeMinuten);

                if (MussWartenAbbrechen(env, mitarbeiterAnforderung, schichtEndeMinuten))
                {
                    brichtWartenAb = true;
                }
                else
                {
                    Request raumAnforderung = schwesterzimmer.Request();
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
                        foreach (Event ev in FuehreSchwesterBehandlungDurch(
                            env, patientId, schwestern, patientenTyp, ankunftszeit, hatTermin,
                            betrittWarteschlange, interneBewegungsdauer, behandlungsdauer,
                            daten, prognoseStatus))
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
                foreach (Event ev in BrichSchwesterWartenAb(env, daten, patientId, null, interneBewegungsdauer, ergebnis, prognoseStatus))
                    yield return ev;
            }
        }

        private static IEnumerable<Event> FuehreSchwesterBehandlungDurch(
            Simulation env,
            int patientId,
            BeweglicherSchwesterPool schwestern,
            PatientenTyp patientenTyp,
            double ankunftszeit,
            bool hatTermin,
            bool betrittWarteschlange,
            TimeSpan interneBewegungsdauer,
            double behandlungsdauer,
            SimulationsDaten daten,
            PrognoseRessourcenStatus prognoseStatus)
        {
            int schwesterId = schwestern.UebernehmeFreienMitarbeiter();

            double jetztMinuten = AktuelleSimulationszeitMinuten(env);
            if (betrittWarteschlange)
            {
                daten.LogEvent(jetztMinuten, "verlaesst_wartezimmer_schwester", patientId);
            }

            daten.LogEvent(jetztMinuten, "geht_zur_schwester", patientId, bewegungsdauerMinuten: interneBewegungsdauer.TotalMinutes);
            yield return env.Timeout(interneBewegungsdauer);

            jetztMinuten = AktuelleSimulationszeitMinuten(env);
            daten.LogEvent(jetztMinuten, "betritt_schwesterzimmer", patientId);
            daten.LogEvent(jetztMinuten, "startet_schwester_prozess", patientId, schwesterId: schwesterId);
            prognoseStatus.StarteBehandlung(patientId, jetztMinuten, behandlungsdauer);

            double wartezeitSchwester = jetztMinuten - ankunftszeit;
            daten.ErfasseSchwesterWartezeit(wartezeitSchwester, patientenTyp, hatTermin);

            daten.ErfasseSchwesterBehandlungszeit(behandlungsdauer, hatTermin, patientenTyp);
            // Eine begonnene Behandlung laeuft bis zum Ende; die Schichtgrenze wird nur beim Warten geprueft.
            yield return env.Timeout(TimeSpan.FromMinutes(behandlungsdauer));

            jetztMinuten = AktuelleSimulationszeitMinuten(env);
            daten.LogEvent(jetztMinuten, "beendet_schwester_prozess", patientId, schwesterId: schwesterId);
            prognoseStatus.BeendeBehandlung(patientId);
            schwestern.GibMitarbeiterZurueck(schwesterId);
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
            double jetztMinuten = AktuelleSimulationszeitMinuten(env);
            prognoseStatus.EntfernePatient(patientId);
            // Hit/Miss jetzt nur noch: Abbruch wegen Feierabend.
            daten.ErfasseSchwesterAbbruchFeierabend(env.StartDate);
            daten.LogEvent(jetztMinuten, "bricht_ab_wegen_feierabend_schwester", patientId, schwesterId: schwesterId);

            daten.LogEvent(jetztMinuten, "geht_zum_ausgang", patientId, bewegungsdauerMinuten: interneBewegungsdauer.TotalMinutes);
            yield return env.Timeout(interneBewegungsdauer);

            jetztMinuten = AktuelleSimulationszeitMinuten(env);
            daten.LogEvent(jetztMinuten, "verlaesst_klinik", patientId);
            daten.SchliessePrognosen(patientId, jetztMinuten);
            ergebnis.MarkiereKlinikVerlassen();
        }
    }
}
