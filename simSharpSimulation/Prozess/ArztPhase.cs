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
                PatientenTyp.Kurz => 3,
                PatientenTyp.Mittel => 2,
                PatientenTyp.Lang => 1,
                _ => 1
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
            PriorityResource arzt,
            int arztId,
            PatientenTyp patientenTyp,
            double ankunftszeit,
            bool hatTermin,
            TimeSpan interneBewegungsdauer,
            Random rnd,
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
                foreach (Event ev in VerschiebeWegenArztAufFolgetag(env, daten, patientId, arztId, hatTermin, interneBewegungsdauer, ergebnis))
                    yield return ev;
                yield break;
            }

            // Schritt A3: Solange kein Arzt frei ist, warten wir solange, bis
            // entweder ein Arzt verfuegbar wird oder die Schicht endet.
            while (arzt.Remaining <= 0)
            {
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                // Im Prognosefenster vor Schichtende pruefen wir, ob die verbleibende
                // Kapazitaet fuer diesen Patienten mit Sicherheitsaufschlag noch ausreicht.
                SchlussplanungsEntscheidung prognose = Schlussplanung.PruefeArzt(env, arzt, patientenTyp, hatTermin);
                if (prognose.MussVerschobenWerden)
                {
                    foreach (Event ev in VerschiebeWegenArztAufFolgetag(env, daten, patientId, arztId, hatTermin, interneBewegungsdauer, ergebnis))
                        yield return ev;
                    yield break;
                }

                // Berechne verbleibende Zeit bis Schichtende und warte entweder
                // auf einen freien Arzt oder auf das Erreichen des Schichtendes.
                double restMinuten = schichtEndeMinuten - nowMinutes;
                if (restMinuten <= 0)
                {
                    foreach (Event ev in VerschiebeWegenArztAufFolgetag(env, daten, patientId, arztId, hatTermin, interneBewegungsdauer, ergebnis))
                        yield return ev;
                    yield break;
                }

                Event arztVerfuegbar = arzt.WhenAny();
                Event schichtEnde = env.Timeout(TimeSpan.FromMinutes(restMinuten));
                yield return arztVerfuegbar | schichtEnde;

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                // Wenn das Schichtende zuerst eintrat, wird der Patient nicht mehr
                // als Feierabend-Abbruch behandelt, sondern geordnet verschoben.
                if (!arztVerfuegbar.IsProcessed)
                {
                    foreach (Event ev in VerschiebeWegenArztAufFolgetag(env, daten, patientId, arztId, hatTermin, interneBewegungsdauer, ergebnis))
                        yield return ev;
                    yield break;
                }
            }

            // Schritt A4: Erst wenn ein Arzt verfuegbar ist, wird der eigentliche Request erstellt.
            // Dadurch vermeiden wir offene Requests, die spaeter kuenstlich aus internen Queues
            // entfernt werden muessten.
            using (Request req = arzt.Request(priority: GetPriority(patientenTyp)))
            {
                // Request abgeschlossen: Patient hat den Arzt zugewiesen bekommen.
                // Selbst wenn die Schicht inzwischen geendet hat, darf die Behandlung
                // weiterlaufen - Patient wird fertig behandelt.
                yield return req;

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

                // Behandlungsdauer nach Patienten-Typ.
                var typInfo = PatientenKonfiguration.TYPEN_VERTEILUNG.First(t => t.Typ == patientenTyp);
                double mittlereDauer = typInfo.BehandlungszeitArzt;
                double variationskoeffizient = typInfo.VariationskoeffizientArzt;

                // Log-Normalverteilung fuer realistischere Zeiten:
                // viele Werte liegen nahe am Mittelwert, einige dauern deutlich laenger.
                // Umrechnung von Mittelwert und Variationskoeffizient in die Parameter mu und sigma der Lognormalverteilung
                double varianz = Math.Pow(variationskoeffizient * mittlereDauer, 2);
                double mu = Math.Log(mittlereDauer) - 0.5 * Math.Log(1 + varianz / Math.Pow(mittlereDauer, 2));
                double sigma = Math.Sqrt(Math.Log(1 + varianz / Math.Pow(mittlereDauer, 2)));

                double dauer = MathNet.Numerics.Distributions.LogNormal.Sample(rnd, mu, sigma);
                daten.ErfasseArztBehandlungszeit(dauer, hatTermin, patientenTyp);

                yield return env.Timeout(TimeSpan.FromMinutes(dauer));

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "beendet_arzt_behandlung", patientId, arztId: arztId);
            }
        }

        private static IEnumerable<Event> VerschiebeWegenArztAufFolgetag(
            Simulation env,
            SimulationsDaten daten,
            int patientId,
            int arztId,
            bool hatTermin,
            TimeSpan interneBewegungsdauer,
            BehandlungsPhaseErgebnis ergebnis)
        {
            // Diese Hilfsmethode haelt den Verschiebungspfad an einer Stelle zusammen,
            // damit Prognosefaelle in allen Arzt-Wartezustaenden identisch behandelt werden.
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.ErfasseArztVerschobenSchlussplanung(env.StartDate);

            string eventTyp = hatTermin
                ? "erhaelt_festen_termin_am_naechsten_vormittag_arzt"
                : "wird_auf_naechsten_tag_verschoben_arzt";
            daten.LogEvent(nowMinutes, eventTyp, patientId, arztId: arztId);

            // Nach der Verschiebung verlaesst der Patient die Klinik ueber den normalen Ausgangspfad.
            daten.LogEvent(nowMinutes, "geht_zum_ausgang", patientId);
            yield return env.Timeout(interneBewegungsdauer);

            nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.LogEvent(nowMinutes, "verlaesst_klinik", patientId);
            ergebnis.MarkiereKlinikVerlassen();
        }
    }
}
