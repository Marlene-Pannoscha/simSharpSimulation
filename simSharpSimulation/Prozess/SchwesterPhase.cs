using SimSharp;
using System;
using System.Collections.Generic;

namespace simSharpSimulation
{
    /*
     * Diese Klasse kapselt die Logik für die "Schwester-Phase" im Simulationsprozess.
     * Sie wird als statische Klasse implementiert, da sie keine eigenen Zustandsdaten hält,
     * sondern alle benötigten Informationen als Parameter erhält.
     */
    public static class SchwesterPhase
    {
        /*
         * Beschreibt den Prozess, den ein Patient bei der Krankenschwester durchläuft.
         *
         * env: Die Simulationsumgebung (Uhr, Ereignis-Scheduler).
         * patientId: Die eindeutige ID des Patienten.
         * schwester: Die Schwester-Ressource, die belegt werden muss.
         * ankunftszeit: Der Zeitpunkt, an dem der Patient die Klinik betreten hat (für Wartezeitberechnung).
         * direktZurSchwester: Gibt an, ob der Patient das Wartezimmer übersprungen hat.
         * pruefeVorbereitungNachZimmer: Steuert, ob eine zufällige Vorbereitung stattfinden soll.
         * wahrscheinlichkeitVorbereitung: Die Wahrscheinlichkeit, dass eine Vorbereitung nötig ist.
         * rnd: Der Zufallsgenerator für stochastische Dauern.
         * daten: Das Objekt zum Sammeln und Speichern von Simulationsdaten.
         * returns: Eine Sequenz von Simulationsereignissen.
         */
        public static IEnumerable<Event> DurchlaufeSchwester(
            Simulation env,
            int patientId,
            Resource schwester,
            double ankunftszeit,
            bool direktZurSchwester,
            bool pruefeVorbereitungNachZimmer,
            double wahrscheinlichkeitVorbereitung,
            Random rnd,
            SimulationsDaten daten)
        {
            // Aktuelle Simulationszeit für das Logging holen.
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;

            // Schritt S1: Wenn der Patient nicht direkt zur Schwester konnte,
            // wird geloggt, dass er die Warteschlange für die Schwester betritt.
            if (!direktZurSchwester)
            {
                daten.LogEvent(nowMinutes, "betritt_schwester_warteschlange", patientId);
            }

            // Schritt S2: Eine Schwester anfordern.
            // 'using' stellt sicher, dass die Ressource am Ende wieder freigegeben wird.
            // Der Prozess pausiert hier (yield return req), bis eine Schwester frei ist.
            using (var req = schwester.Request())
            {
                yield return req; // Warten, bis die Schwester-Ressource verfügbar ist.

                // Schritt S3: Schwester ist frei, der Prozess wird fortgesetzt.
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "startet_schwester_prozess", patientId);

                // Die Wartezeit auf die Schwester berechnen und speichern.
                double wartezeitSchwester = nowMinutes - ankunftszeit;
                daten.SchwesternWartezeiten.Add(wartezeitSchwester);

                // Schritt S4: Prüfen, ob eine Vorbereitung durch die Schwester stattfinden soll.
                // Dies wird durch den aufrufenden Prozess gesteuert.
                if (pruefeVorbereitungNachZimmer)
                {
                    // Zufällig entscheiden, ob eine Vorbereitung tatsächlich notwendig ist.
                    bool brauchtVorbereitung = rnd.NextDouble() < wahrscheinlichkeitVorbereitung;
                    if (brauchtVorbereitung)
                    {
                        // Schritt S4A: Vorbereitungsprozess starten.
                        daten.LogEvent(nowMinutes, "startet_vorbereitung_schwester", patientId);

                        // Dauer der Vorbereitung zufällig bestimmen (Exponentialverteilung).
                        double dauerVorbereitung = MathNet.Numerics.Distributions.Exponential.Sample(rnd, 1.0 / SchwesterKonfiguration.MITTLERE_SCHWESTER_ZEIT);
                        yield return env.Timeout(TimeSpan.FromMinutes(dauerVorbereitung)); // Prozess für die Dauer anhalten.

                        // Vorbereitung ist abgeschlossen.
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "beendet_vorbereitung_schwester", patientId);
                    }
                }

                // Schritt S5: Die eigentliche Behandlung/Interaktion mit der Schwester.
                // Dauer der Behandlung zufällig bestimmen (Exponentialverteilung).
                double dauerBehandlung = MathNet.Numerics.Distributions.Exponential.Sample(rnd, 1.0 / SchwesterKonfiguration.MITTLERE_SCHWESTER_ZEIT);
                yield return env.Timeout(TimeSpan.FromMinutes(dauerBehandlung)); // Prozess für die Dauer anhalten.

                // Schritt S6: Der gesamte Schwester-Prozess ist beendet.
                // Die Ressource wird durch das 'using'-Statement automatisch freigegeben.
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "beendet_schwester_prozess", patientId);
            }
        }
    }
}
