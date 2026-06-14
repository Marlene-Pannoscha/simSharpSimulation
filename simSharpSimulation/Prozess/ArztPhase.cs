using SimSharp;
using System;
using System.Collections.Generic;

namespace simSharpSimulation
{
    public static class ArztPhase
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
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            int prioritaet = GetPriority(patientenTyp);
            prognoseStatus.RegistriereWartend(patientId, behandlungsdauer, prioritaet);

            if (nowMinutes >= schichtEndeMinuten)
            {
                foreach (Event ev in BrichArztWartenAb(env, daten, patientId, null, interneBewegungsdauer, ergebnis, prognoseStatus))
                    yield return ev;
                yield break;
            }

            // Schritt A3: Der Request wird sofort gestellt, damit die PriorityResource
            // die Reihenfolge nach Prioritaet und FIFO korrekt verwalten kann.
            bool brichtWartenAb = false;
            Request req = aerzte.FordereMitarbeiterAn(prioritaet);
            try
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
                    Request raumReq = arztzimmer.Request();
                    try
                    {
                        restMinuten = schichtEndeMinuten - (env.Now - env.StartDate).TotalMinutes;
                        schichtEnde = env.Timeout(TimeSpan.FromMinutes(restMinuten));
                        yield return raumReq | schichtEnde;

                        nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                        if (!raumReq.IsProcessed || nowMinutes >= schichtEndeMinuten)
                        {
                            brichtWartenAb = true;
                        }
                    }
                    finally
                    {
                        DisposeWennVerarbeitet(raumReq);
                    }

                    if (!brichtWartenAb)
                    {
                        int arztId = aerzte.UebernehmeFreienMitarbeiter();

                        // HIT: Ab hier hat der Patient den Arzt tatsaechlich erreicht.
                        daten.ErfasseArztBehandlungBegonnen(env.StartDate);

                        nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                        // Patient verlaesst das Wartezimmer, sobald der Arzt frei ist.
                        daten.LogEvent(nowMinutes, "verlaesst_wartezimmer_fuer_arzt", patientId);

                        // Weg zum Arzt (interne Bewegung).
                        daten.LogEvent(nowMinutes, "geht_zum_arzt", patientId);
                        yield return env.Timeout(interneBewegungsdauer);

                        nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                        daten.LogEvent(nowMinutes, "betritt_arztzimmer", patientId);

                        // Arztbehandlung beginnt.
                        daten.LogEvent(nowMinutes, "startet_arzt_behandlung", patientId, arztId: arztId);
                        prognoseStatus.StarteBehandlung(patientId, nowMinutes, behandlungsdauer);

                        // Wartezeit erfassen.
                        double wartezeitArzt = nowMinutes - ankunftszeit;
                        daten.ErfasseArztWartezeit(wartezeitArzt, hatTermin, patientenTyp);

                        daten.ErfasseArztBehandlungszeit(behandlungsdauer, hatTermin, patientenTyp);

                        yield return env.Timeout(TimeSpan.FromMinutes(behandlungsdauer));

                        nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                        daten.LogEvent(nowMinutes, "beendet_arzt_behandlung", patientId, arztId: arztId);
                        prognoseStatus.BeendeBehandlung(patientId);
                        aerzte.GibMitarbeiterZurueck(arztId);
                    }
                }
            }
            finally
            {
                DisposeWennVerarbeitet(req);
            }

            if (brichtWartenAb)
            {
                foreach (Event ev in BrichArztWartenAb(env, daten, patientId, null, interneBewegungsdauer, ergebnis, prognoseStatus))
                    yield return ev;
            }
        }

        private static void DisposeWennVerarbeitet(Request request)
        {
            if (request.IsProcessed)
                request.Dispose();
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
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            prognoseStatus.EntfernePatient(patientId);
            // Hit/Miss jetzt nur noch: Abbruch wegen Feierabend.
            daten.ErfasseArztAbbruchFeierabend(env.StartDate);
            daten.LogEvent(nowMinutes, "bricht_ab_wegen_feierabend_arzt", patientId, arztId: arztId);

            daten.LogEvent(nowMinutes, "geht_zum_ausgang", patientId);
            yield return env.Timeout(interneBewegungsdauer);

            nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.LogEvent(nowMinutes, "verlaesst_klinik", patientId);
            daten.SchliessePrognosen(patientId, nowMinutes);
            ergebnis.MarkiereKlinikVerlassen();
        }
    }
}
