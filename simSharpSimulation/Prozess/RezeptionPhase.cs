using SimSharp;
using System;
using System.Collections.Generic;

namespace simSharpSimulation
{
    /*
     * Diese Klasse kapselt die Logik für die "Rezeptions-Phase" im Simulationsprozess.
     * Sie ist statisch, da sie keine eigenen Zustandsdaten speichert und alle Informationen
     * über Parameter erhält.
     */
    public static class RezeptionPhase
    {
        /*
         * Beschreibt den Prozess, den ein Patient an der Rezeption durchläuft.
         * Von der Ankunft in der Warteschlange bis zum Abschluss der Anmeldung.
         *
         * env: Die Simulationsumgebung, die die Zeit und Ereignisse steuert.
         * patientId: Die eindeutige ID des Patienten für das Logging.
         * rezeption: Die Rezeptions-Ressource, die belegt werden muss.
         * ankunftszeit: Der Zeitpunkt des Klinik-Eintritts zur Wartezeitberechnung.
         * hatTermin: Gibt an, ob der Patient einen Termin hat (wird für spätere Logs benötigt).
         * rnd: Der globale Zufallsgenerator für die Dauer der Bedienung.
         * daten: Das Objekt zum Sammeln aller relevanten Simulationsdaten.
         * returns: Eine Sequenz von Simulationsereignissen, die den Ablauf steuern.
         */
        public static IEnumerable<Event> DurchlaufeRezeption(
            Simulation env,
            int patientId,
            Resource rezeption,
            double ankunftszeit,
            bool hatTermin,
            Random rnd,
            SimulationsDaten daten)
        {
            // Aktuelle Simulationszeit für das Logging holen.
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;

            // Schritt R1: Patient stellt sich in die Warteschlange für die Rezeption.
            daten.LogEvent(nowMinutes, "betritt_rezeption_warteschlange", patientId);

            // Schritt R2: Einen Rezeptionisten anfordern.
            // 'using' stellt sicher, dass die Ressource (der Rezeptionist) nach der Nutzung
            // automatisch wieder für den nächsten Patienten freigegeben wird.
            using (var req = rezeption.Request())
            {
                // Der Prozess pausiert hier, bis ein Rezeptionist frei ist.
                yield return req;

                // Schritt R3: Rezeptionist ist frei, die Bedienung beginnt.
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "startet_rezeption", patientId);

                // Die Wartezeit an der Rezeption berechnen und für die Statistik speichern.
                double wartezeitRezeption = nowMinutes - ankunftszeit;
                daten.WartezeitenRezeption.Add(wartezeitRezeption);

                // Schritt R4: Dauer der Bedienung an der Rezeption simulieren.
                // Die Dauer wird zufällig aus einer Exponentialverteilung gezogen.
                double dauer = MathNet.Numerics.Distributions.Exponential.Sample(rnd, 1.0 / RezeptionKonfiguration.MITTLERE_DAUER);
                
                // Die Simulation wird für die berechnete Dauer angehalten.
                yield return env.Timeout(TimeSpan.FromMinutes(dauer));

                // Schritt R5: Die Bedienung ist abgeschlossen.
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "beendet_rezeption", patientId, new Dictionary<string, object> { { "hat_termin", hatTermin } });
            }
            // Die Ressource wird hier durch 'using' automatisch freigegeben.
        }
    }
}

