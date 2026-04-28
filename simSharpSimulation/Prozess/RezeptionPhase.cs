using SimSharp;
using System;
using System.Collections.Generic;
using System.Reflection;

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
            bool behandlungBereitsFertig,
            Random rnd,
            SimulationsDaten daten)
        {
            // Aktuelle Simulationszeit für das Logging holen.
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;

            // Schritt R1: Patient stellt sich in die Warteschlange für die Rezeption.
            daten.LogEvent(nowMinutes, "betritt_rezeption_warteschlange", patientId);

            bool rezeptionWarFrei = IstRezeptionFrei(rezeption);
            daten.LogEvent(nowMinutes, rezeptionWarFrei ? "rezeption_frei" : "rezeption_nicht_frei", patientId);
            if (!rezeptionWarFrei)
            {
                daten.LogEvent(nowMinutes, "wartet_in_rezeption_warteschlange", patientId);
            }

            // Schritt R2: Einen Rezeptionisten anfordern.
            // 'using' stellt sicher, dass die Ressource (der Rezeptionist) nach der Nutzung
            // automatisch wieder für den nächsten Patienten freigegeben wird.
            using (var req = rezeption.Request())
            {
                // Der Prozess pausiert hier, bis ein Rezeptionist frei ist.
                yield return req;

                // Schritt R3: Rezeptionist ist frei, die Bedienung beginnt.
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                if (!rezeptionWarFrei)
                {
                    daten.LogEvent(nowMinutes, "rezeption_frei", patientId);
                }
                daten.LogEvent(nowMinutes, "betritt_rezeption", patientId);
                daten.LogEvent(nowMinutes, behandlungBereitsFertig ? "behandlung_bereits_fertig" : "behandlung_nicht_fertig", patientId);
                daten.LogEvent(nowMinutes, "startet_rezeption", patientId);

                // Die Wartezeit an der Rezeption berechnen und für die Statistik speichern.
                double wartezeitRezeption = nowMinutes - ankunftszeit;
                daten.ErfasseRezeptionWartezeit(wartezeitRezeption, hatTermin);

                // Schritt R4: Dauer der Bedienung an der Rezeption simulieren.
                // Die Dauer wird zufällig aus einer Exponentialverteilung gezogen.
                double dauer = MathNet.Numerics.Distributions.Exponential.Sample(rnd, 1.0 / RezeptionKonfiguration.MITTELREZEPTIONSZEIT);
                daten.ErfasseRezeptionBehandlungszeit(dauer, hatTermin);

                // Die Simulation wird für die berechnete Dauer angehalten.
                yield return env.Timeout(TimeSpan.FromMinutes(dauer));

                // Schritt R5: Die Bedienung ist abgeschlossen.
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "beendet_rezeption", patientId);
                if (behandlungBereitsFertig)
                {
                    daten.LogEvent(nowMinutes, "macht_folgetermin_aus_oder_rezept", patientId);
                }
                else
                {
                    daten.LogEvent(nowMinutes, hatTermin ? "rezeption_hat_termin" : "rezeption_ohne_termin", patientId);
                }
            }
            // Die Ressource wird hier durch 'using' automatisch freigegeben.
        }

        private static bool IstRezeptionFrei(Resource rezeption)
        {
            var usersProperty = rezeption.GetType().GetProperty("Users", BindingFlags.NonPublic | BindingFlags.Instance);
            var usersCollection = usersProperty?.GetValue(rezeption) as IReadOnlyCollection<Request>;
            int aktiveNutzer = usersCollection?.Count ?? 0;
            return aktiveNutzer < RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN;
        }
    }
}

