using SimSharp;
using System;
using System.Collections.Generic;

namespace simSharpSimulation
{
    /*
     * Diese Klasse kapselt die Logik für die "Arzt-Phase" im Simulationsprozess.
     * Sie ist als statische Klasse implementiert, da sie keine eigenen Zustandsdaten hält
     * und alle notwendigen Informationen über Parameter erhält.
     */
    public static class ArztPhase
    {
        /*
         * Beschreibt den Prozess, den ein Patient beim Arzt durchläuft.
         * Von der Ankunft in der Warteschlange bis zum Ende der Behandlung.
         *
         * env: Die Simulationsumgebung, die Zeit und Ereignisse verwaltet.
         * patientId: Die eindeutige ID des Patienten für das Logging.
         * arzt: Die Arzt-Ressource, die belegt werden muss.
         * ankunftszeit: Der Zeitpunkt des Klinik-Eintritts zur Berechnung der Wartezeit.
         * rnd: Der Zufallsgenerator für die Behandlungsdauer.
         * daten: Das Objekt zum Sammeln und Speichern von Simulationsdaten.
         * returns: Eine Sequenz von Simulationsereignissen, die den Ablauf steuern.
         */
        public static IEnumerable<Event> DurchlaufeArzt(
            Simulation env,
            int patientId,
            Resource arzt,
            double ankunftszeit,
            Random rnd,
            SimulationsDaten daten)
        {
            // Aktuelle Simulationszeit für das Logging holen.
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;

            // Schritt A1: Patient stellt sich in die Warteschlange für den Arzt.
            //daten.LogEvent(nowMinutes, "betritt_arzt_warteschlange", patientId);

            // Schritt A2: Einen Arzt anfordern.
            // 'using' sorgt dafür, dass die Ressource (der Arzt) nach der Behandlung
            // automatisch wieder für den nächsten Patienten freigegeben wird.
            using (var req = arzt.Request())
            {
                // Der Prozess pausiert hier (yield return), bis ein Arzt verfügbar ist.
                yield return req;

                // Schritt A3: Arzt ist frei, die Behandlung beginnt.
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "startet_arzt_behandlung", patientId);

                // Die Wartezeit auf den Arzt berechnen und für die Statistik speichern.
                double wartezeitArzt = nowMinutes - ankunftszeit;
                daten.Wartezeiten.Add(wartezeitArzt);

                // Schritt A4: Dauer der ärztlichen Behandlung simulieren.
                // Die Dauer wird zufällig aus einer Exponentialverteilung bestimmt.
                double dauer = MathNet.Numerics.Distributions.Exponential.Sample(rnd, 1.0 / ArztKonfiguration.MITTLERE_BEHANDLUNGSZEIT);
                
                // Die Simulation wird für die berechnete Dauer angehalten.
                yield return env.Timeout(TimeSpan.FromMinutes(dauer));

                // Schritt A5: Die Behandlung ist abgeschlossen.
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "beendet_arzt_behandlung", patientId);
            }
            // Die Arzt-Ressource wird hier durch das 'using'-Statement automatisch freigegeben.
        }
    }
}
