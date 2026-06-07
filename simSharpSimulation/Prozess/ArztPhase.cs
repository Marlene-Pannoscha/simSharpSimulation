using SimSharp;
using System;
using System.Collections.Generic;

namespace simSharpSimulation
{
    /*
     * Diese Klasse kapselt die Logik fuer die "Arzt-Phase" im Simulationsprozess.
     * Sie ist als statische Klasse implementiert, da sie keine eigenen Zustandsdaten haelt
     * und alle notwendigen Informationen ueber Parameter erhaelt.
     */
    public static class ArztPhase
    {
        private static int GetPriority(PatientenTyp typ)
        {
            // Kuerzere Behandlungen erhalten eine hoehere Prioritaet, damit der Arzt
            // kurze Faelle schneller abarbeiten kann und die Warteschlange beweglich bleibt.
            return typ switch
            {
                PatientenTyp.Kurz => 1,
                PatientenTyp.Mittel => 2,
                PatientenTyp.Lang => 3,
                _ => 3
            };
        }

        /*
         * Beschreibt den Prozess, den ein Patient beim Arzt durchlaeuft.
         * Von der Ankunft in der Warteschlange bis zum Ende der Behandlung.
         *
         * env: Die Simulationsumgebung, die Zeit und Ereignisse verwaltet.
         * patientId: Die eindeutige ID des Patienten fuer das Logging.
         * arzt: Die Arzt-Ressource, die belegt werden muss.
         * ankunftszeit: Der Zeitpunkt des Klinik-Eintritts zur Berechnung der Wartezeit.
         * rnd: Der Zufallsgenerator fuer die Behandlungsdauer.
         * daten: Das Objekt zum Sammeln und Speichern von Simulationsdaten.
         * returns: Eine Sequenz von Simulationsereignissen, die den Ablauf steuern.
         */
        public static IEnumerable<Event> DurchlaufeArzt(
            Simulation env,
            int patientId,
            BeweglicherArztPool aerzte,
            PatientenTyp patientenTyp,
            double ankunftszeit,
            bool hatTermin,
            TimeSpan interneBewegungsdauer,
            double behandlungsdauer,
            SimulationsDaten daten,
            BehandlungsPhaseErgebnis ergebnis)
        {
            // Aktuelle Simulationszeit fuer das Logging holen.
            double schichtEndeMinuten = SimulationKonfiguration.SIMULATIONSDAUER;
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;

            // Schritt A2: Vor dem eigentlichen Warten wird geprueft, ob der Arztbereich
            // fuer diesen Tag ueberhaupt noch offen ist.
            if (nowMinutes >= schichtEndeMinuten)
            {
                foreach (Event ev in BrichArztWartenAb(env, daten, patientId, null, interneBewegungsdauer, ergebnis))
                    yield return ev;
                yield break;
            }

            // Schritt A3: Der Request wird sofort gestellt, damit die PriorityResource
            // die Reihenfolge nach Prioritaet und FIFO korrekt verwalten kann.
            bool brichtWartenAb = false;
            using (Request req = aerzte.FordereMitarbeiterAn(GetPriority(patientenTyp)))
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

                    // Wartezeit erfassen.
                    double wartezeitArzt = nowMinutes - ankunftszeit;
                    daten.ErfasseArztWartezeit(wartezeitArzt, hatTermin, patientenTyp);

                    daten.ErfasseArztBehandlungszeit(behandlungsdauer, hatTermin, patientenTyp);

                    yield return env.Timeout(TimeSpan.FromMinutes(behandlungsdauer));

                    nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                    daten.LogEvent(nowMinutes, "beendet_arzt_behandlung", patientId, arztId: arztId);
                    aerzte.GibMitarbeiterZurueck(arztId);
                }
            }

            if (brichtWartenAb)
            {
                foreach (Event ev in BrichArztWartenAb(env, daten, patientId, null, interneBewegungsdauer, ergebnis))
                    yield return ev;
            }
        }

        private static IEnumerable<Event> BrichArztWartenAb(
            Simulation env,
            SimulationsDaten daten,
            int patientId,
            int? arztId,
            TimeSpan interneBewegungsdauer,
            BehandlungsPhaseErgebnis ergebnis)
        {
            // Diese Hilfsmethode haelt den Abbruchpfad an einer Stelle zusammen,
            // damit Wartezeit- und Feierabend-Abbrueche identisch behandelt werden.
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            // Hit/Miss jetzt nur noch: Abbruch wegen Feierabend.
            daten.ErfasseArztAbbruchFeierabend(env.StartDate);
            daten.LogEvent(nowMinutes, "bricht_ab_wegen_feierabend_arzt", patientId, arztId: arztId);

            // Nach dem Abbruch verlaesst der Patient die Klinik ueber den normalen Ausgangspfad.
            daten.LogEvent(nowMinutes, "geht_zum_ausgang", patientId);
            yield return env.Timeout(interneBewegungsdauer);

            nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.LogEvent(nowMinutes, "verlaesst_klinik", patientId);
            daten.SchliessePrognosen(patientId, nowMinutes);
            ergebnis.MarkiereKlinikVerlassen();
        }
    }
}
